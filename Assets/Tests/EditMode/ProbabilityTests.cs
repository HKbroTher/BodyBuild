using System;
using MassFactory.Core;
using NUnit.Framework;

namespace MassFactory.Tests
{
    public class ProbabilityTests
    {
        private static readonly InjectionOutcome[] AllOutcomes =
        {
            InjectionOutcome.BodyDestroyed, InjectionOutcome.Atrophy, InjectionOutcome.NoEffect,
            InjectionOutcome.Growth, InjectionOutcome.Mutation
        };

        [Test]
        public void BaseTable_AtLevelZero_MatchesSpec()
        {
            var table = InjectionProbabilityCalculator.CalculateBase(new InjectionConfig(), 0);
            Assert.AreEqual(3.0, table[InjectionOutcome.BodyDestroyed], 1e-9);
            Assert.AreEqual(12.0, table[InjectionOutcome.Atrophy], 1e-9);
            Assert.AreEqual(11.0, table[InjectionOutcome.NoEffect], 1e-9);
            Assert.AreEqual(62.0, table[InjectionOutcome.Growth], 1e-9);
            Assert.AreEqual(12.0, table[InjectionOutcome.Mutation], 1e-9);
        }

        [Test]
        public void BaseTable_AtLevelTen_HasNoMutationLeft()
        {
            var table = InjectionProbabilityCalculator.CalculateBase(new InjectionConfig(), 10);
            Assert.AreEqual(35.0, table[InjectionOutcome.BodyDestroyed], 1e-9);
            Assert.AreEqual(42.0, table[InjectionOutcome.Growth], 1e-9);
            Assert.AreEqual(0.0, table[InjectionOutcome.Mutation], 1e-9);
        }

        [Test]
        public void DestroyChance_IsClampedBetween3And65()
        {
            var config = new InjectionConfig();
            Assert.AreEqual(3.0, InjectionProbabilityCalculator.DestroyChance(config, 0), 1e-9);
            Assert.AreEqual(3.0 + 5 * 3.2, InjectionProbabilityCalculator.DestroyChance(config, 5), 1e-9);
            Assert.AreEqual(65.0, InjectionProbabilityCalculator.DestroyChance(config, 30), 1e-9);
            Assert.AreEqual(65.0, InjectionProbabilityCalculator.DestroyChance(config, 1000), 1e-9);
        }

        [Test]
        public void BaseTable_AlwaysSumsTo100_AndIsNonNegative([Range(0, 60, 3)] int level)
        {
            var table = InjectionProbabilityCalculator.CalculateBase(new InjectionConfig(), level);
            Assert.AreEqual(100.0, table.Total, 1e-9);
            foreach (var outcome in AllOutcomes) Assert.GreaterOrEqual(table[outcome], 0.0);
        }

        [Test]
        public void BaseTable_WithOversizedFixedChances_StillSumsTo100()
        {
            var config = new InjectionConfig { AtrophyChance = 80, NoEffectChance = 80, MinDestroyChance = 50, MaxDestroyChance = 50 };
            var table = InjectionProbabilityCalculator.CalculateBase(config, 0);
            Assert.AreEqual(100.0, table.Total, 1e-9);
            Assert.AreEqual(50.0, table[InjectionOutcome.BodyDestroyed], 1e-9);
            Assert.AreEqual(50.0, table[InjectionOutcome.Atrophy], 1e-9);
        }

        [Test]
        public void Normalize_RandomTables_SumExactly100()
        {
            var rng = new SeededRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                var table = new OutcomeTable();
                foreach (var outcome in AllOutcomes) table[outcome] = rng.NextDouble() * 300;
                table.Normalize();
                Assert.AreEqual(100.0, table.Total, 1e-9);
            }
        }

        [Test]
        public void Normalize_EmptyTable_BecomesNoEffect()
        {
            var table = new OutcomeTable();
            table.Normalize();
            Assert.AreEqual(100.0, table[InjectionOutcome.NoEffect], 1e-9);
        }

        [Test]
        public void Shift_MovesOnlyWhatIsAvailable()
        {
            var table = new OutcomeTable();
            table[InjectionOutcome.Growth] = 4;
            table[InjectionOutcome.NoEffect] = 10;
            double moved = table.Shift(InjectionOutcome.Mutation, 6, InjectionOutcome.Growth, InjectionOutcome.NoEffect);
            Assert.AreEqual(6.0, moved, 1e-9);
            Assert.AreEqual(0.0, table[InjectionOutcome.Growth], 1e-9);
            Assert.AreEqual(8.0, table[InjectionOutcome.NoEffect], 1e-9);
            Assert.AreEqual(6.0, table[InjectionOutcome.Mutation], 1e-9);
        }

        [Test]
        public void Roll_UsesCumulativeRanges()
        {
            var table = InjectionProbabilityCalculator.CalculateBase(new InjectionConfig(), 0);
            Assert.AreEqual(InjectionOutcome.BodyDestroyed, table.Roll(0.0));
            Assert.AreEqual(InjectionOutcome.BodyDestroyed, table.Roll(0.0299));
            Assert.AreEqual(InjectionOutcome.Atrophy, table.Roll(0.03));
            Assert.AreEqual(InjectionOutcome.NoEffect, table.Roll(0.15));
            Assert.AreEqual(InjectionOutcome.Growth, table.Roll(0.26));
            Assert.AreEqual(InjectionOutcome.Mutation, table.Roll(0.88));
            Assert.AreEqual(InjectionOutcome.Mutation, table.Roll(0.999999));
        }

        [Test]
        public void Roll_NeverPicksZeroWeightOutcome()
        {
            var table = InjectionProbabilityCalculator.CalculateBase(new InjectionConfig(), 0);
            table[InjectionOutcome.BodyDestroyed] = 0;
            table.Normalize();
            var rng = new SeededRandom(3);
            for (int i = 0; i < 20000; i++)
                Assert.AreNotEqual(InjectionOutcome.BodyDestroyed, table.Roll(rng.NextDouble()));
        }

        [Test]
        public void Roll_FrequenciesMatchTable()
        {
            var table = InjectionProbabilityCalculator.CalculateBase(new InjectionConfig(), 5);
            var rng = new SeededRandom(2024);
            var counts = new int[5];
            const int samples = 200000;
            for (int i = 0; i < samples; i++) counts[(int)table.Roll(rng.NextDouble())]++;
            foreach (var outcome in AllOutcomes)
                Assert.AreEqual(table[outcome], counts[(int)outcome] * 100.0 / samples, 0.5, outcome.ToString());
        }

        [Test]
        public void SeededRandom_IsDeterministic_AndInRange()
        {
            var a = new SeededRandom(99);
            var b = new SeededRandom(99);
            for (int i = 0; i < 1000; i++)
            {
                double x = a.NextDouble();
                Assert.AreEqual(x, b.NextDouble());
                Assert.GreaterOrEqual(x, 0.0);
                Assert.Less(x, 1.0);
            }
            var resumed = SeededRandom.FromState(a.State);
            Assert.AreEqual(a.NextDouble(), resumed.NextDouble());
        }

        [Test]
        public void SeededRandom_Next_RejectsNonPositiveBound()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SeededRandom(1).Next(0));
        }
    }
}
