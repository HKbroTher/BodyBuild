using System.Collections.Generic;
using System.Reflection;
using MassFactory.Core;

namespace MassFactory.Tests
{
    internal static class TestHelpers
    {
        /// <summary>Default config with a single, easy competition list unless thresholds are given.</summary>
        public static GameConfig Config(params long[] thresholds)
        {
            var config = GameConfig.CreateDefault();
            if (thresholds.Length > 0)
            {
                config.Competitions.Clear();
                for (int i = 0; i < thresholds.Length; i++)
                    config.Competitions.Add(new CompetitionDefinition("Comp " + (i + 1), thresholds[i], 5));
            }
            return config;
        }

        /// <summary>Rigs the probability model so every injection produces the given outcome.</summary>
        public static void Force(GameConfig config, InjectionOutcome outcome)
        {
            var inj = config.Injection;
            inj.BaseDestroyChance = 0;
            inj.DestroyChancePerLevel = 0;
            inj.MinDestroyChance = 0;
            inj.MaxDestroyChance = 0;
            inj.AtrophyChance = 0;
            inj.NoEffectChance = 0;
            inj.MaxGrowthChance = 0;
            switch (outcome)
            {
                case InjectionOutcome.BodyDestroyed:
                    inj.BaseDestroyChance = 100;
                    inj.MinDestroyChance = 100;
                    inj.MaxDestroyChance = 100;
                    break;
                case InjectionOutcome.Atrophy: inj.AtrophyChance = 100; break;
                case InjectionOutcome.NoEffect: inj.NoEffectChance = 100; break;
                case InjectionOutcome.Growth: inj.MaxGrowthChance = 100; break;
                case InjectionOutcome.Mutation: break; // everything left over goes to MUTATION
            }
        }

        public static GameConfig Forced(InjectionOutcome outcome, params long[] thresholds)
        {
            var config = Config(thresholds);
            Force(config, outcome);
            return config;
        }

        public static GameSession NewSession(GameConfig config, long seed = 42)
        {
            return GameSession.NewRun(config, RelicCatalog.CreateDefault(), seed);
        }

        public static RelicDefinition Relic(GameSession session, string id)
        {
            return session.Catalog.Get(id);
        }

        public static AthleteData First(GameSession session)
        {
            return session.Roster[0];
        }

        /// <summary>Reflection deep copy for serializable data classes (stands in for JsonUtility in tests).</summary>
        public static T DeepCopy<T>(T source) where T : class
        {
            return (T)Copy(source);
        }

        private static object Copy(object source)
        {
            if (source == null) return null;
            var type = source.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)) return source;
            if (source is System.Collections.IList list)
            {
                var copy = (System.Collections.IList)System.Activator.CreateInstance(type);
                foreach (var item in list) copy.Add(Copy(item));
                return copy;
            }
            var result = System.Activator.CreateInstance(type);
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                field.SetValue(result, Copy(field.GetValue(source)));
            return result;
        }

        public static string Fingerprint(GameSession session)
        {
            var s = session.CaptureState();
            var parts = new List<string>
            {
                s.Phase.ToString(), s.RoundIndex.ToString(), s.SubmittedScore.ToString(), s.Research.ToString(),
                s.RngState.ToString(), s.Stats.Injections.ToString(), s.Stats.Destroyed.ToString()
            };
            foreach (var a in s.Roster) parts.Add(a.AthleteID + ":" + a.MassLevel + ":" + a.CurrentScore);
            return string.Join("|", parts.ToArray());
        }
    }
}
