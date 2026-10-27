using System;

namespace MassFactory.Core
{
    /// <summary>Baseline probability model, independent of relics, rush and scene objects.</summary>
    public static class InjectionProbabilityCalculator
    {
        public static double DestroyChance(InjectionConfig config, int massLevel)
        {
            double raw = config.BaseDestroyChance + Math.Max(0, massLevel) * config.DestroyChancePerLevel;
            return Clamp(raw, config.MinDestroyChance, config.MaxDestroyChance);
        }

        /// <summary>
        /// Allocates probability in priority order: BODY DESTROYED, ATROPHY, NO EFFECT,
        /// GROWTH (capped), and MUTATION takes whatever remains. Result sums to 100.
        /// </summary>
        public static OutcomeTable CalculateBase(InjectionConfig config, int massLevel)
        {
            double destroy = Clamp(DestroyChance(config, massLevel), 0, 100);
            double budget = 100.0 - destroy;

            double atrophy = Math.Min(Math.Max(0, config.AtrophyChance), budget);
            budget -= atrophy;
            double noEffect = Math.Min(Math.Max(0, config.NoEffectChance), budget);
            budget -= noEffect;
            double growth = Math.Min(Math.Max(0, config.MaxGrowthChance), budget);
            budget -= growth;
            double mutation = Math.Max(0, budget);

            var table = new OutcomeTable();
            table[InjectionOutcome.BodyDestroyed] = destroy;
            table[InjectionOutcome.Atrophy] = atrophy;
            table[InjectionOutcome.NoEffect] = noEffect;
            table[InjectionOutcome.Growth] = growth;
            table[InjectionOutcome.Mutation] = mutation;
            table.Normalize();
            return table;
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
