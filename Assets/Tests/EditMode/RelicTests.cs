using MassFactory.Core;
using NUnit.Framework;

namespace MassFactory.Tests
{
    public class RelicTests
    {
        private static GameSession WithRelic(GameConfig config, string relicId, long seed = 42)
        {
            var session = TestHelpers.NewSession(config, seed);
            Assert.IsTrue(session.Relics.Add(TestHelpers.Relic(session, relicId)), relicId);
            return session;
        }

        [Test]
        public void DefaultCatalog_HasAtLeast12UniqueRelics_WithEffects()
        {
            var catalog = RelicCatalog.CreateDefault();
            Assert.GreaterOrEqual(catalog.All.Count, 12);
            foreach (var relic in catalog.All)
            {
                Assert.IsNotNull(RelicEffectFactory.Create(relic.EffectType), relic.Id);
                Assert.Greater(relic.Cost, 0, relic.Id);
                Assert.IsFalse(string.IsNullOrEmpty(relic.Description), relic.Id);
            }
        }

        [Test]
        public void GoldenSyringe_IncreasesMutationChance()
        {
            var plain = TestHelpers.NewSession(TestHelpers.Config(100000));
            var boosted = WithRelic(TestHelpers.Config(100000), "golden_syringe");
            double before = plain.PreviewOutcomes(TestHelpers.First(plain))[InjectionOutcome.Mutation];
            double after = boosted.PreviewOutcomes(TestHelpers.First(boosted))[InjectionOutcome.Mutation];
            Assert.AreEqual(before + 6, after, 1e-9);
            Assert.AreEqual(100.0, boosted.PreviewOutcomes(TestHelpers.First(boosted)).Total, 1e-9);
        }

        [Test]
        public void BlackMarketFridge_ProtectsOnlyFirstInjectionEachRound()
        {
            var session = WithRelic(TestHelpers.Forced(InjectionOutcome.BodyDestroyed, 100000, 100000), "black_market_fridge");
            var athlete = TestHelpers.First(session);
            Assert.AreEqual(0.0, session.PreviewDestroyChance(athlete), 1e-9);
            var first = session.Inject(athlete.AthleteID);
            Assert.IsFalse(first.Value.AthleteDestroyed);
            Assert.AreEqual(100.0, session.PreviewDestroyChance(athlete), 1e-9);
            Assert.IsTrue(session.Inject(athlete.AthleteID).Value.AthleteDestroyed);
        }

        [Test]
        public void OlympiaCoach_RecruitsStartAtLevelOne()
        {
            var config = TestHelpers.Config(500, 100000);
            var session = WithRelic(config, "olympia_coach");
            session.Submit(TestHelpers.First(session).AthleteID);
            session.AdvanceRound();
            int newcomers = 0;
            foreach (var athlete in session.Roster)
            {
                if (athlete.RecruitedInRound != 1) continue;
                newcomers++;
                Assert.AreEqual(1, athlete.MassLevel);
                Assert.AreEqual(750, athlete.CurrentScore);
            }
            Assert.AreEqual(config.Roster.NewAthletesPerRound, newcomers);
        }

        [Test]
        public void MutantGenetics_TurnsAtrophyIntoGrowth()
        {
            var session = TestHelpers.NewSession(TestHelpers.Forced(InjectionOutcome.Atrophy, 100000));
            var relic = TestHelpers.Relic(session, "mutant_genetics");
            relic.Value = 100; // guarantee the conversion for the test
            session.Relics.Add(relic);
            var result = session.Inject(TestHelpers.First(session).AthleteID).Value;
            Assert.AreEqual(InjectionOutcome.Atrophy, result.RolledOutcome);
            Assert.AreEqual(InjectionOutcome.Growth, result.Outcome);
            Assert.AreEqual("Mutant Genetics", result.TransformedBy);
            Assert.AreEqual(1, result.LevelAfter);
        }

        [Test]
        public void FakeMuscleOil_MultipliesScore_AndAddsRisk()
        {
            var plain = TestHelpers.NewSession(TestHelpers.Config(100000));
            var oiled = WithRelic(TestHelpers.Config(100000), "fake_muscle_oil");
            var athlete = TestHelpers.First(oiled);
            Assert.AreEqual(750, athlete.CurrentScore, "existing athletes are re-scored");
            Assert.AreEqual(plain.PreviewDestroyChance(TestHelpers.First(plain)) + 4, oiled.PreviewDestroyChance(athlete), 1e-9);
            Assert.AreEqual(750, oiled.Submit(athlete.AthleteID).Value);
        }

        [Test]
        public void UndergroundLaboratory_BuffsNextRecruitAfterDeaths()
        {
            var config = TestHelpers.Forced(InjectionOutcome.BodyDestroyed, 500, 100000);
            config.Roster.NewAthletesPerRound = 3;
            var session = WithRelic(config, "underground_laboratory");
            session.Inject(session.Roster[0].AthleteID);
            session.Inject(session.Roster[0].AthleteID);
            session.Submit(session.Roster[0].AthleteID);
            session.AdvanceRound();
            Assert.AreEqual(2, session.Roster[0].MassLevel, "first new recruit receives the stored bonus");
            Assert.AreEqual(0, session.Roster[1].MassLevel);
        }

        [Test]
        public void HumanRecyclingProgram_RecruitsAfterThreeDeaths()
        {
            var config = TestHelpers.Forced(InjectionOutcome.BodyDestroyed, 100000);
            config.Roster.NewAthletesPerRound = 4;
            var session = WithRelic(config, "human_recycling_program");
            int bonus = 0;
            session.Events.Subscribe<AthleteRecruitedEvent>(e => { if (e.IsBonusRecruit) bonus++; });
            for (int i = 0; i < 3; i++) session.Inject(session.Roster[0].AthleteID);
            Assert.AreEqual(1, bonus);
            Assert.AreEqual(2, session.Roster.Count);
            Assert.AreEqual(RunPhase.InProgress, session.Phase);
        }

        [Test]
        public void CorporateSponsor_ConvertsExcessScoreToResearch()
        {
            var config = TestHelpers.Forced(InjectionOutcome.Growth, 1000, 100000);
            var session = WithRelic(config, "corporate_sponsor");
            var athlete = TestHelpers.First(session);
            for (int i = 0; i < 7; i++) session.Inject(athlete.AthleteID); // 8,543
            session.Submit(athlete.AthleteID);
            int before = session.State.Research;
            session.AdvanceRound();
            // excess 7,543 -> 7 research from the sponsor + 5 competition reward
            Assert.AreEqual(before + 7 + 5, session.State.Research);
        }

        [Test]
        public void ForbiddenFormula_MultipliesAthleteScoreOnMutation()
        {
            var session = WithRelic(TestHelpers.Forced(InjectionOutcome.Mutation, 100000), "forbidden_formula");
            var athlete = TestHelpers.First(session);
            var result = session.Inject(athlete.AthleteID).Value;
            Assert.AreEqual(1.25, athlete.PersonalScoreMultiplier, 1e-9);
            Assert.AreEqual(ScoreCalculator.Calculate(500, 1.5, 2, 1.25), athlete.CurrentScore);
            Assert.AreEqual(athlete.CurrentScore, result.ScoreAfter);
        }

        [Test]
        public void SecondHeart_PreventsOneDestruction_ThenIsConsumed()
        {
            var session = WithRelic(TestHelpers.Forced(InjectionOutcome.BodyDestroyed, 100000), "second_heart");
            var athlete = TestHelpers.First(session);
            Assert.IsTrue(session.Relics.PreventsNextDestruction(athlete));
            var saved = session.Inject(athlete.AthleteID).Value;
            Assert.AreEqual(InjectionOutcome.BodyDestroyed, saved.RolledOutcome);
            Assert.AreEqual(InjectionOutcome.NoEffect, saved.Outcome);
            Assert.IsTrue(athlete.IsAlive);
            Assert.IsFalse(session.Relics.Has("second_heart"));
            Assert.AreEqual(0, session.State.Relics.Count);
            Assert.IsTrue(session.Inject(athlete.AthleteID).Value.AthleteDestroyed);
        }

        [Test]
        public void CheapNeedles_ReducesPrices()
        {
            var session = TestHelpers.NewSession(TestHelpers.Config(100000));
            var coach = TestHelpers.Relic(session, "olympia_coach");
            int full = session.Shop.PriceOf(coach);
            session.Relics.Add(TestHelpers.Relic(session, "cheap_needles"));
            Assert.AreEqual(full * 3 / 4 + (full * 3 % 4 == 0 ? 0 : 1), session.Shop.PriceOf(coach));
        }

        [Test]
        public void NoPainNoGain_BoostsAfterConsecutiveAtrophy()
        {
            var session = WithRelic(TestHelpers.Config(100000), "no_pain_no_gain");
            var athlete = TestHelpers.First(session);
            var baseTable = session.PreviewOutcomes(athlete);
            athlete.ConsecutiveAtrophy = 2;
            var boosted = session.PreviewOutcomes(athlete);
            double good = boosted[InjectionOutcome.Growth] + boosted[InjectionOutcome.Mutation];
            double goodBefore = baseTable[InjectionOutcome.Growth] + baseTable[InjectionOutcome.Mutation];
            Assert.AreEqual(goodBefore + 16, good, 1e-9);
            Assert.AreEqual(baseTable[InjectionOutcome.BodyDestroyed], boosted[InjectionOutcome.BodyDestroyed], 1e-9);
        }

        [Test]
        public void RushRelics_ModifyThresholdAndDuration()
        {
            var config = TestHelpers.Config(100000);
            var session = TestHelpers.NewSession(config);
            int threshold = session.Rush.Threshold;
            int duration = session.Rush.Duration;
            session.Relics.Add(TestHelpers.Relic(session, "adrenaline_pump"));
            session.Relics.Add(TestHelpers.Relic(session, "overclocked_adrenals"));
            Assert.AreEqual(threshold - 2, session.Rush.Threshold);
            Assert.AreEqual(duration + 2, session.Rush.Duration);
        }

        [Test]
        public void Fridge_StillProtects_WhenOilIsBoughtAfterIt()
        {
            var session = WithRelic(TestHelpers.Config(100000), "black_market_fridge");
            session.Relics.Add(TestHelpers.Relic(session, "fake_muscle_oil"));
            Assert.AreEqual(0.0, session.PreviewDestroyChance(TestHelpers.First(session)), 1e-9);
        }
    }
}
