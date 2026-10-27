using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MassFactory.Core;
using MassFactory.Core.Simulation;

namespace MassFactory.Sim
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            if (args.Length > 0 && args[0] == "--sweep")
                return Sweep(args.Length > 1 ? int.Parse(args[1]) : 1000);

            int runs = args.Length > 0 ? int.Parse(args[0]) : 5000;
            long seed = args.Length > 1 ? long.Parse(args[1]) : 12345;
            string output = args.Length > 2 ? args[2] : null;

            var catalog = RelicCatalog.CreateDefault();
            var sb = new StringBuilder();
            sb.AppendLine(FullReport("Tuned default (GameConfig.CreateDefault)", GameConfig.CreateDefault(), catalog, runs, seed));
            sb.AppendLine(FullReport("Spec baseline (GameConfig.CreateSpecBaseline)", GameConfig.CreateSpecBaseline(), catalog, runs, seed));

            string text = sb.ToString();
            Console.WriteLine(text);
            if (!string.IsNullOrEmpty(output))
            {
                File.WriteAllText(output, text);
                Console.WriteLine("Report written to " + output);
            }
            return 0;
        }

        private static string FullReport(string title, GameConfig config, RelicCatalog catalog, int runs, long seed)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# " + title);
            sb.AppendLine();
            sb.AppendLine("Thresholds: " + Thresholds(config) + "; recruits/round " + config.Roster.NewAthletesPerRound +
                          "; roster cap " + config.Roster.RosterCapacity + "; rush threshold " + config.Rush.ActivationThreshold + ".");
            sb.AppendLine();
            sb.AppendLine("## Single athlete analysis (no relics, no rush)");
            sb.AppendLine();
            sb.AppendLine(SingleAthleteTable(config, 200000, seed));

            var strategies = new List<Func<ISimulationStrategy>>
            {
                () => RiskThresholdStrategy.Conservative(),
                () => RiskThresholdStrategy.Balanced(),
                () => RiskThresholdStrategy.Aggressive(),
                () => new ScoreTargetStrategy(),
                () => new RiskThresholdStrategy("Conservative", 15) { SacrificeSurplusKeep = 1 },
            };

            var reports = new List<SimulationReport>();
            var runner = new SimulationRunner(config, catalog) { ScenarioName = "Shop on" };
            foreach (var factory in strategies) reports.Add(runner.Run(factory, runs, seed));

            var noShop = new SimulationRunner(config, catalog) { ScenarioName = "No shop" };
            reports.Add(noShop.Run(() => new RiskThresholdStrategy("Balanced", 25) { UseShop = false }, runs, seed));
            reports.Add(noShop.Run(() => new ScoreTargetStrategy { UseShop = false }, runs, seed));

            // Stress test: every relic at once (ignores slot cap) to look for infinite loops / runaway values.
            var allRelics = new SimulationRunner(config, catalog)
            {
                ScenarioName = "All relics",
                SetupSession = s => { foreach (var relic in catalog.All) s.Relics.Add(relic); }
            };
            reports.Add(allRelics.Run(() => new RiskThresholdStrategy("Balanced", 25) { UseShop = false }, runs, seed));
            reports.Add(allRelics.Run(() => new RiskThresholdStrategy("Aggressive", 35) { UseShop = false }, runs, seed));

            sb.AppendLine("## Full run simulation (" + runs + " runs per row, seeds " + seed + ".." + (seed + runs - 1) + ")");
            sb.AppendLine();
            sb.AppendLine("Strategies (all buy the best-ranked affordable relic and use the rush when the next destroy chance is >= 12%):");
            sb.AppendLine();
            foreach (var factory in strategies)
            {
                var s = factory();
                sb.AppendLine("- **" + s.Name + "**: " + s.Description);
            }
            sb.AppendLine();
            sb.AppendLine(SimulationReportFormatter.ToMarkdown(reports, config));

            sb.AppendLine("Where runs die (share of all runs failing at each competition):");
            sb.AppendLine();
            sb.AppendLine("| Strategy | Scenario | " + RoundHeader(config));
            sb.AppendLine("|---|---|" + Repeat("---:|", config.Competitions.Count));
            foreach (var r in reports)
            {
                sb.Append("| ").Append(r.StrategyName).Append(" | ").Append(r.Scenario).Append(" |");
                for (int i = 1; i <= config.Competitions.Count; i++)
                {
                    r.FailedAtRound.TryGetValue(i, out int failed);
                    sb.AppendFormat(" {0:P1} |", (double)failed / r.Runs);
                }
                sb.AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine("Loop / runaway checks:");
            sb.AppendLine();
            foreach (var r in reports)
                sb.AppendLine("- " + r.StrategyName + " / " + r.Scenario + ": max recruits in one round = " + r.MaxRecruitsInOneRound +
                              ", action-cap hits = " + r.ActionCapHits + ", stalled runs = " + r.StalledRuns);
            sb.AppendLine();

            var aggressive = reports.Find(r => r.StrategyName == "Aggressive" && r.Scenario == "Shop on");
            sb.AppendLine(SimulationReportFormatter.HistogramMarkdown("Aggressive (shop on): MassLevel at destruction",
                aggressive.DestroyedAtLevel, aggressive.DestroyedAthletes));
            return sb.ToString();
        }

        private static string Thresholds(GameConfig config)
        {
            var parts = new List<string>();
            foreach (var c in config.Competitions) parts.Add(c.RequiredScore.ToString("N0"));
            return string.Join(" / ", parts.ToArray());
        }

        /// <summary>Quick tuning sweep: win rate and round reach of several config variants.</summary>
        private static int Sweep(int runs)
        {
            var catalog = RelicCatalog.CreateDefault();
            var variants = new List<KeyValuePair<string, Action<GameConfig>>>
            {
                new KeyValuePair<string, Action<GameConfig>>("spec", c => { }),
                new KeyValuePair<string, Action<GameConfig>>("A+rw", c => { SetThresholds(c, 2000, 4000, 7000, 11000, 17000, 26000, 40000); c.Roster.NewAthletesPerRound = 4; SetRewards(c, 6, 8, 10, 12, 14, 16, 0); }),
                new KeyValuePair<string, Action<GameConfig>>("A+rw+rush6", c => { SetThresholds(c, 2000, 4000, 7000, 11000, 17000, 26000, 40000); c.Roster.NewAthletesPerRound = 4; SetRewards(c, 6, 8, 10, 12, 14, 16, 0); c.Rush.ActivationThreshold = 6; }),
                new KeyValuePair<string, Action<GameConfig>>("E: 32k+rw+rush6", c => { SetThresholds(c, 2000, 3500, 6000, 9000, 14000, 21000, 32000); c.Roster.NewAthletesPerRound = 4; SetRewards(c, 6, 8, 10, 12, 14, 16, 0); c.Rush.ActivationThreshold = 6; }),
                new KeyValuePair<string, Action<GameConfig>>("F: 50k+rw+rush6+cap8", c => { SetThresholds(c, 2000, 4000, 7500, 12000, 19000, 30000, 50000); c.Roster.NewAthletesPerRound = 4; c.Roster.RosterCapacity = 8; SetRewards(c, 6, 8, 10, 12, 14, 16, 0); c.Rush.ActivationThreshold = 6; }),
            };
            var strategies = new List<Func<ISimulationStrategy>>
            {
                () => RiskThresholdStrategy.Conservative(),
                () => RiskThresholdStrategy.Balanced(),
                () => RiskThresholdStrategy.Aggressive(),
                () => new ScoreTargetStrategy(),
            };

            Console.WriteLine("| Variant | Strategy | Win | R1 | R2 | R3 | R4 | R5 | R6 | R7 |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var variant in variants)
            {
                var config = GameConfig.CreateSpecBaseline();
                variant.Value(config);
                var runner = new SimulationRunner(config, catalog);
                foreach (var factory in strategies)
                {
                    var r = runner.Run(factory, runs, 777);
                    var sb = new StringBuilder();
                    sb.AppendFormat("| {0} | {1} | {2:P1} |", variant.Key, r.StrategyName, r.WinRate);
                    for (int i = 0; i < config.Competitions.Count; i++) sb.AppendFormat(" {0:P0} |", r.RoundCompletionRate(i));
                    Console.WriteLine(sb.ToString());
                }
            }
            return 0;
        }

        private static void SetRewards(GameConfig config, params int[] rewards)
        {
            for (int i = 0; i < rewards.Length && i < config.Competitions.Count; i++)
                config.Competitions[i].ResearchReward = rewards[i];
        }

        private static void SetThresholds(GameConfig config, params long[] thresholds)
        {
            for (int i = 0; i < thresholds.Length && i < config.Competitions.Count; i++)
                config.Competitions[i].RequiredScore = thresholds[i];
        }

        /// <summary>Monte Carlo of a lone athlete injected until a target level: survival and expected score.</summary>
        private static string SingleAthleteTable(GameConfig config, int trials, long seed)
        {
            var rng = new SeededRandom((ulong)seed);
            var sb = new StringBuilder();
            sb.AppendLine("Inject one fresh athlete until it reaches the target MassLevel (or dies).");
            sb.AppendLine();
            sb.AppendLine("| Target lvl | Destroy % at lvl | Score at lvl | P(reach) | Avg injections | Expected score | Expected research if sacrificed |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|");
            for (int target = 1; target <= 14; target++)
            {
                int reached = 0;
                long injections = 0;
                for (int t = 0; t < trials; t++)
                {
                    int level = 0;
                    bool alive = true;
                    int guard = 0;
                    while (alive && level < target && guard++ < 1000)
                    {
                        var table = InjectionProbabilityCalculator.CalculateBase(config.Injection, level);
                        var outcome = table.Roll(rng.NextDouble());
                        injections++;
                        switch (outcome)
                        {
                            case InjectionOutcome.BodyDestroyed: alive = false; break;
                            case InjectionOutcome.Atrophy: level = Math.Max(0, level - config.Injection.AtrophyLevels); break;
                            case InjectionOutcome.Growth: level += config.Injection.GrowthLevels; break;
                            case InjectionOutcome.Mutation: level += config.Injection.MutationLevels; break;
                        }
                    }
                    if (alive) reached++;
                }
                double p = (double)reached / trials;
                long score = ScoreCalculator.Calculate(config.Athletes.BaseScore, config.Athletes.LevelMultiplier, target);
                sb.AppendFormat("| {0} | {1:0.0} | {2:N0} | {3:P1} | {4:0.0} | {5:N0} | {6:0.0} |",
                    target, InjectionProbabilityCalculator.DestroyChance(config.Injection, target), score, p,
                    (double)injections / trials, p * score, p * Math.Floor(score / config.Economy.ScorePerResearch));
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("Note: reaching a level by MUTATION can overshoot the target, so the real score is slightly higher than listed.");
            return sb.ToString();
        }

        private static string RoundHeader(GameConfig config)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < config.Competitions.Count; i++) sb.Append("R").Append(i + 1).Append(" | ");
            return sb.ToString().TrimEnd();
        }

        private static string Repeat(string s, int n)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++) sb.Append(s);
            return sb.ToString();
        }
    }
}
