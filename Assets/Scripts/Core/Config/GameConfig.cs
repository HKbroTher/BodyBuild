using System;
using System.Collections.Generic;

namespace MassFactory.Core
{
    /// <summary>Tuning for the injection probability model. All chances are percentages (0-100).</summary>
    [Serializable]
    public class InjectionConfig
    {
        public double BaseDestroyChance = 3.0;
        public double DestroyChancePerLevel = 3.2;
        public double MinDestroyChance = 3.0;
        public double MaxDestroyChance = 65.0;
        public double AtrophyChance = 12.0;
        public double NoEffectChance = 11.0;
        public double MaxGrowthChance = 62.0;

        public int MutationLevels = 2;
        public int GrowthLevels = 1;
        public int AtrophyLevels = 1;
    }

    [Serializable]
    public class AthleteConfig
    {
        public double BaseScore = 500.0;
        public double LevelMultiplier = 1.5;
        public int StartingMassLevel = 0;
    }

    [Serializable]
    public class RosterConfig
    {
        public int NewAthletesPerRound = 3;
        public int RosterCapacity = 6;
    }

    [Serializable]
    public class EconomyConfig
    {
        /// <summary>ResearchGained = Floor(CurrentScore / ScorePerResearch).</summary>
        public double ScorePerResearch = 500.0;
        public int StartingResearch = 0;
        public int ShopChoices = 3;
        public int MaxRelics = 8;
        public int RestockBaseCost = 3;
        public int RestockCostIncrease = 2;
    }

    [Serializable]
    public class RushConfig
    {
        public bool Enabled = true;
        public int ActivationThreshold = 8;
        public int DurationInjections = 3;
        public int EnergyPerGrowth = 1;
        public int EnergyPerMutation = 2;
        public int MinActivationThreshold = 2;
    }

    [Serializable]
    public class CompetitionDefinition
    {
        public string Name;
        public long RequiredScore;
        public int ResearchReward;

        public CompetitionDefinition() { }

        public CompetitionDefinition(string name, long requiredScore, int researchReward)
        {
            Name = name;
            RequiredScore = requiredScore;
            ResearchReward = researchReward;
        }
    }

    /// <summary>
    /// Root tuning object. Plain serializable data so it can live inside a Unity ScriptableObject
    /// and be built in code for tests and simulations.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        /// <summary>Hard ceiling for any single score value, keeps arithmetic far away from overflow.</summary>
        public const long MaxScore = 1_000_000_000_000_000L;

        public AthleteConfig Athletes = new AthleteConfig();
        public InjectionConfig Injection = new InjectionConfig();
        public RosterConfig Roster = new RosterConfig();
        public EconomyConfig Economy = new EconomyConfig();
        public RushConfig Rush = new RushConfig();
        public List<CompetitionDefinition> Competitions = new List<CompetitionDefinition>();

        /// <summary>
        /// Default tuning used by the game. Derived from the spec baseline after simulation
        /// (see Docs/BalancingReport.md): the baseline thresholds double every round while athlete supply
        /// is flat, which made every simulated strategy lose (0% wins in thousands of runs).
        /// </summary>
        public static GameConfig CreateDefault()
        {
            var config = CreateSpecBaseline();
            config.Roster.NewAthletesPerRound = 4;
            config.Roster.RosterCapacity = 8;
            config.Rush.ActivationThreshold = 6;
            long[] thresholds = { 2_000, 4_000, 7_500, 12_000, 19_000, 30_000, 50_000 };
            int[] rewards = { 6, 8, 10, 12, 14, 16, 0 };
            for (int i = 0; i < config.Competitions.Count; i++)
            {
                config.Competitions[i].RequiredScore = thresholds[i];
                config.Competitions[i].ResearchReward = rewards[i];
            }
            return config;
        }

        /// <summary>The initial values from the design brief, kept for comparison in simulations.</summary>
        public static GameConfig CreateSpecBaseline()
        {
            var config = new GameConfig();
            config.Competitions.Add(new CompetitionDefinition("Local Underground", 2_000, 4));
            config.Competitions.Add(new CompetitionDefinition("Backstreet Classic", 5_000, 5));
            config.Competitions.Add(new CompetitionDefinition("Iron District Open", 10_000, 6));
            config.Competitions.Add(new CompetitionDefinition("Chemical Cup", 20_000, 7));
            config.Competitions.Add(new CompetitionDefinition("Freak Show Invitational", 40_000, 8));
            config.Competitions.Add(new CompetitionDefinition("Underground Olympia", 80_000, 10));
            config.Competitions.Add(new CompetitionDefinition("Mr. Mutation", 150_000, 0));
            return config;
        }

        public GameConfig Clone()
        {
            var copy = new GameConfig
            {
                Athletes = (AthleteConfig)ShallowCopy(Athletes),
                Injection = (InjectionConfig)ShallowCopy(Injection),
                Roster = (RosterConfig)ShallowCopy(Roster),
                Economy = (EconomyConfig)ShallowCopy(Economy),
                Rush = (RushConfig)ShallowCopy(Rush),
                Competitions = new List<CompetitionDefinition>()
            };
            foreach (var c in Competitions)
                copy.Competitions.Add(new CompetitionDefinition(c.Name, c.RequiredScore, c.ResearchReward));
            return copy;
        }

        /// <summary>Returns a list of human readable problems; empty when the config is usable.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (Athletes == null || Injection == null || Roster == null || Economy == null || Rush == null)
            {
                errors.Add("Config sections must not be null.");
                return errors;
            }
            if (Athletes.BaseScore <= 0) errors.Add("Athletes.BaseScore must be positive.");
            if (Athletes.LevelMultiplier < 1.0) errors.Add("Athletes.LevelMultiplier must be >= 1.");
            if (Athletes.StartingMassLevel < 0) errors.Add("Athletes.StartingMassLevel must be >= 0.");
            if (Injection.MinDestroyChance < 0 || Injection.MaxDestroyChance > 100 || Injection.MinDestroyChance > Injection.MaxDestroyChance)
                errors.Add("Injection destroy chance bounds must satisfy 0 <= Min <= Max <= 100.");
            if (Injection.AtrophyChance < 0 || Injection.NoEffectChance < 0 || Injection.MaxGrowthChance < 0)
                errors.Add("Injection chances must be non-negative.");
            if (Roster.NewAthletesPerRound < 1) errors.Add("Roster.NewAthletesPerRound must be >= 1.");
            if (Roster.RosterCapacity < 1) errors.Add("Roster.RosterCapacity must be >= 1.");
            if (Economy.ScorePerResearch <= 0) errors.Add("Economy.ScorePerResearch must be positive.");
            if (Economy.ShopChoices < 1) errors.Add("Economy.ShopChoices must be >= 1.");
            if (Economy.MaxRelics < 1) errors.Add("Economy.MaxRelics must be >= 1.");
            if (Rush.ActivationThreshold < 1 || Rush.DurationInjections < 1) errors.Add("Rush threshold and duration must be >= 1.");
            if (Competitions == null || Competitions.Count == 0) errors.Add("At least one competition is required.");
            else
            {
                for (int i = 0; i < Competitions.Count; i++)
                {
                    if (Competitions[i] == null || Competitions[i].RequiredScore <= 0)
                        errors.Add("Competition " + (i + 1) + " needs a positive RequiredScore.");
                }
            }
            return errors;
        }

        private static object ShallowCopy(object source)
        {
            var type = source.GetType();
            var copy = Activator.CreateInstance(type);
            foreach (var field in type.GetFields())
                field.SetValue(copy, field.GetValue(source));
            return copy;
        }
    }
}
