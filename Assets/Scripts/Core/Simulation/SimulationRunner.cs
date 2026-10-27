using System;
using System.Collections.Generic;
using System.Text;

namespace MassFactory.Core.Simulation
{
    /// <summary>Aggregated metrics for many simulated runs of one strategy.</summary>
    public sealed class SimulationReport
    {
        public string StrategyName;
        public string StrategyDescription;
        public string Scenario;
        public int Runs;
        public int Victories;
        public int StalledRuns;
        public int ActionCapHits;
        /// <summary>RoundsReached[i] = runs that completed at least i+1 competitions.</summary>
        public int[] RoundsCompleted;
        public long TotalInjections;
        public long TotalAthletes;
        public long DestroyedAthletes;
        public long SubmittedAthletes;
        public long SacrificedAthletes;
        public long LeftoverAthletes;
        public long TotalSubmittedScore;
        public long TotalResearchEarned;
        public long TotalRelicsBought;
        public int MaxRecruitsInOneRound;
        public readonly Dictionary<int, int> DestroyedAtLevel = new Dictionary<int, int>();
        public readonly Dictionary<int, int> SubmittedAtLevel = new Dictionary<int, int>();
        public readonly Dictionary<int, int> FailedAtRound = new Dictionary<int, int>();

        public double WinRate => Runs == 0 ? 0 : (double)Victories / Runs;

        public double AverageDestroyLevel => WeightedAverage(DestroyedAtLevel);
        public double AverageSubmitLevel => WeightedAverage(SubmittedAtLevel);
        public double AverageScorePerSubmitted => SubmittedAthletes == 0 ? 0 : (double)TotalSubmittedScore / SubmittedAthletes;
        public double SurvivalRate => TotalAthletes == 0 ? 0 : 1.0 - (double)DestroyedAthletes / TotalAthletes;

        public double RoundCompletionRate(int round)
        {
            return Runs == 0 ? 0 : (double)RoundsCompleted[round] / Runs;
        }

        private static double WeightedAverage(Dictionary<int, int> histogram)
        {
            long count = 0, sum = 0;
            foreach (var pair in histogram)
            {
                count += pair.Value;
                sum += (long)pair.Key * pair.Value;
            }
            return count == 0 ? 0 : (double)sum / count;
        }
    }

    public sealed class SimulationRunner
    {
        public int MaxActionsPerRun = 20000;

        /// <summary>Optional hook to modify each new session (e.g. grant relics for a scenario).</summary>
        public Action<GameSession> SetupSession;
        public string ScenarioName = "Default";

        private readonly GameConfig _config;
        private readonly RelicCatalog _catalog;

        public SimulationRunner(GameConfig config, RelicCatalog catalog)
        {
            _config = config;
            _catalog = catalog;
        }

        public SimulationReport Run(Func<ISimulationStrategy> strategyFactory, int runs, long baseSeed)
        {
            var probe = strategyFactory();
            var report = new SimulationReport
            {
                StrategyName = probe.Name,
                StrategyDescription = probe.Description,
                Scenario = ScenarioName,
                Runs = runs,
                RoundsCompleted = new int[_config.Competitions.Count]
            };

            for (int i = 0; i < runs; i++)
                RunOne(strategyFactory(), baseSeed + i, report);
            return report;
        }

        private void RunOne(ISimulationStrategy strategy, long seed, SimulationReport report)
        {
            var session = GameSession.NewRun(_config, _catalog, seed);
            int recruitsThisRound = 0;

            session.Events.Subscribe<AthleteRecruitedEvent>(e =>
            {
                report.TotalAthletes++;
                recruitsThisRound++;
                if (recruitsThisRound > report.MaxRecruitsInOneRound) report.MaxRecruitsInOneRound = recruitsThisRound;
            });
            session.Events.Subscribe<RoundStartedEvent>(e => recruitsThisRound = 0);
            session.Events.Subscribe<AthleteDestroyedEvent>(e =>
            {
                report.DestroyedAthletes++;
                Increment(report.DestroyedAtLevel, e.LevelAtDestruction);
            });
            session.Events.Subscribe<AthleteSubmittedEvent>(e =>
            {
                report.SubmittedAthletes++;
                report.TotalSubmittedScore += e.Score;
                Increment(report.SubmittedAtLevel, e.Athlete.MassLevel);
            });
            session.Events.Subscribe<AthleteSacrificedEvent>(e => report.SacrificedAthletes++);
            session.Events.Subscribe<RelicPurchasedEvent>(e => report.TotalRelicsBought++);

            // Starting recruits were published before we subscribed.
            report.TotalAthletes += session.Roster.Count;
            recruitsThisRound = session.Roster.Count;
            if (recruitsThisRound > report.MaxRecruitsInOneRound) report.MaxRecruitsInOneRound = recruitsThisRound;

            SetupSession?.Invoke(session);

            int actions = 0;
            while (!session.IsRunOver)
            {
                if (actions++ >= MaxActionsPerRun)
                {
                    report.ActionCapHits++;
                    break;
                }
                if (!strategy.Step(session))
                {
                    report.StalledRuns++;
                    break;
                }
            }

            report.TotalInjections += session.State.Stats.Injections;
            report.TotalResearchEarned += session.State.Stats.ResearchEarned;
            report.LeftoverAthletes += session.ActiveAthleteCount;
            int completed = session.State.Stats.RoundsCompleted;
            for (int r = 0; r < completed && r < report.RoundsCompleted.Length; r++) report.RoundsCompleted[r]++;
            if (session.Phase == RunPhase.Victory) report.Victories++;
            else Increment(report.FailedAtRound, session.State.RoundIndex + 1);
        }

        private static void Increment(Dictionary<int, int> histogram, int key)
        {
            histogram.TryGetValue(key, out int value);
            histogram[key] = value + 1;
        }
    }

    public static class SimulationReportFormatter
    {
        public static string ToMarkdown(IList<SimulationReport> reports, GameConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| Strategy | Scenario | Runs | Win rate | Avg injections | Survival | Avg destroy lvl | Avg submit lvl | Avg score / submitted | Avg research | Relics bought | Stalls | Cap hits |");
            sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var r in reports)
            {
                sb.AppendFormat("| {0} | {1} | {2} | {3:P1} | {4:0.0} | {5:P1} | {6:0.00} | {7:0.00} | {8:N0} | {9:0.0} | {10:0.00} | {11} | {12} |",
                    r.StrategyName, r.Scenario, r.Runs, r.WinRate, (double)r.TotalInjections / r.Runs, r.SurvivalRate,
                    r.AverageDestroyLevel, r.AverageSubmitLevel, r.AverageScorePerSubmitted,
                    (double)r.TotalResearchEarned / r.Runs, (double)r.TotalRelicsBought / r.Runs, r.StalledRuns, r.ActionCapHits);
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("Round completion probability (share of runs that cleared each competition):");
            sb.AppendLine();
            sb.Append("| Strategy | Scenario |");
            for (int i = 0; i < config.Competitions.Count; i++) sb.Append(" R").Append(i + 1).Append(" |");
            sb.AppendLine();
            sb.Append("|---|---|");
            for (int i = 0; i < config.Competitions.Count; i++) sb.Append("---:|");
            sb.AppendLine();
            foreach (var r in reports)
            {
                sb.Append("| ").Append(r.StrategyName).Append(" | ").Append(r.Scenario).Append(" |");
                for (int i = 0; i < config.Competitions.Count; i++) sb.AppendFormat(" {0:P1} |", r.RoundCompletionRate(i));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string HistogramMarkdown(string title, Dictionary<int, int> histogram, long total)
        {
            var keys = new List<int>(histogram.Keys);
            keys.Sort();
            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine();
            sb.AppendLine("| Level | Count | Share |");
            sb.AppendLine("|---:|---:|---:|");
            foreach (var key in keys)
                sb.AppendFormat("| {0} | {1} | {2:P1} |", key, histogram[key], total == 0 ? 0 : (double)histogram[key] / total).AppendLine();
            return sb.ToString();
        }
    }
}
