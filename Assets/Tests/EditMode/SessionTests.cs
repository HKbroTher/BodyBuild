using MassFactory.Core;
using NUnit.Framework;

namespace MassFactory.Tests
{
    public class SessionTests
    {
        [Test]
        public void NewRun_RecruitsConfiguredAthletes_WithSpecStats()
        {
            var config = TestHelpers.Config(1000);
            var session = TestHelpers.NewSession(config);
            Assert.AreEqual(config.Roster.NewAthletesPerRound, session.Roster.Count);
            foreach (var athlete in session.Roster)
            {
                Assert.AreEqual(0, athlete.MassLevel);
                Assert.AreEqual(500.0, athlete.BaseScore);
                Assert.AreEqual(500, athlete.CurrentScore);
                Assert.AreEqual(0, athlete.InjectionCount);
                Assert.IsTrue(athlete.IsAlive);
                Assert.IsFalse(athlete.IsSubmitted);
                Assert.IsFalse(athlete.IsSacrificed);
                Assert.IsFalse(string.IsNullOrEmpty(athlete.DisplayName));
            }
            Assert.AreEqual(session.Roster[0].AthleteID, session.State.SelectedAthleteId);
        }

        [Test]
        public void Growth_And_Mutation_IncreaseLevelAndScore()
        {
            var session = TestHelpers.NewSession(TestHelpers.Forced(InjectionOutcome.Growth, 100000));
            var athlete = TestHelpers.First(session);
            var result = session.Inject(athlete.AthleteID);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(InjectionOutcome.Growth, result.Value.Outcome);
            Assert.AreEqual(1, athlete.MassLevel);
            Assert.AreEqual(750, athlete.CurrentScore);
            Assert.AreEqual(1, athlete.InjectionCount);

            TestHelpers.Force(session.Config, InjectionOutcome.Mutation);
            session.Inject(athlete.AthleteID);
            Assert.AreEqual(3, athlete.MassLevel);
            Assert.AreEqual(1688, athlete.CurrentScore);
        }

        [Test]
        public void Atrophy_NeverDropsBelowZero()
        {
            var session = TestHelpers.NewSession(TestHelpers.Forced(InjectionOutcome.Atrophy, 100000));
            var athlete = TestHelpers.First(session);
            session.Inject(athlete.AthleteID);
            session.Inject(athlete.AthleteID);
            Assert.AreEqual(0, athlete.MassLevel);
            Assert.AreEqual(2, athlete.ConsecutiveAtrophy);
        }

        [Test]
        public void NoEffect_KeepsLevel()
        {
            var session = TestHelpers.NewSession(TestHelpers.Forced(InjectionOutcome.NoEffect, 100000));
            var athlete = TestHelpers.First(session);
            var result = session.Inject(athlete.AthleteID);
            Assert.AreEqual(InjectionOutcome.NoEffect, result.Value.Outcome);
            Assert.AreEqual(0, athlete.MassLevel);
        }

        [Test]
        public void Destroyed_AthleteIsRemoved_AndCannotBeInjectedAgain()
        {
            var session = TestHelpers.NewSession(TestHelpers.Forced(InjectionOutcome.BodyDestroyed, 100000));
            var athlete = TestHelpers.First(session);
            int id = athlete.AthleteID;
            int before = session.Roster.Count;

            var result = session.Inject(id);
            Assert.IsTrue(result.Value.AthleteDestroyed);
            Assert.IsFalse(athlete.IsAlive);
            Assert.AreEqual(before - 1, session.Roster.Count);
            Assert.IsNull(session.GetAthlete(id));
            Assert.AreEqual(0, result.Value.ScoreAfter);

            Assert.IsFalse(session.Inject(id).Success);
            Assert.IsFalse(session.Submit(id).Success);
            Assert.IsFalse(session.Sacrifice(id).Success);
            Assert.AreNotEqual(id, session.State.SelectedAthleteId, "selection moves to a living athlete");
        }

        [Test]
        public void Submit_AddsScore_RemovesAthlete_AndIsIrreversible()
        {
            var session = TestHelpers.NewSession(TestHelpers.Config(100000));
            var athlete = TestHelpers.First(session);
            var result = session.Submit(athlete.AthleteID);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(500, result.Value);
            Assert.AreEqual(500, session.Competition.SubmittedScore);
            Assert.IsTrue(athlete.IsSubmitted);
            Assert.IsNull(session.GetAthlete(athlete.AthleteID));
            Assert.IsFalse(session.Submit(athlete.AthleteID).Success, "double submission is rejected");
            Assert.AreEqual(500, session.Competition.SubmittedScore);
        }

        [Test]
        public void Sacrifice_GrantsFloorOfScoreOver500()
        {
            var session = TestHelpers.NewSession(TestHelpers.Forced(InjectionOutcome.Growth, 100000));
            var athlete = TestHelpers.First(session);
            for (int i = 0; i < 5; i++) session.Inject(athlete.AthleteID); // level 5 = 3,797
            int before = session.State.Research;
            var result = session.Sacrifice(athlete.AthleteID);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(7, result.Value);
            Assert.AreEqual(before + 7, session.State.Research);
            Assert.IsTrue(athlete.IsSacrificed);
            Assert.IsFalse(session.Sacrifice(athlete.AthleteID).Success);
        }

        [Test]
        public void Advance_RequiresThreshold()
        {
            var session = TestHelpers.NewSession(TestHelpers.Config(1000, 5000));
            Assert.IsFalse(session.CanAdvance);
            Assert.IsFalse(session.AdvanceRound().Success);
            session.Submit(session.Roster[0].AthleteID);
            Assert.IsFalse(session.CanAdvance);
            session.Submit(session.Roster[0].AthleteID);
            Assert.IsTrue(session.CanAdvance);
        }

        [Test]
        public void Advance_RewardsResets_RecruitsAndKeepsLeftovers()
        {
            var config = TestHelpers.Config(1000, 5000);
            var session = TestHelpers.NewSession(config);
            session.Submit(session.Roster[0].AthleteID);
            session.Submit(session.Roster[0].AthleteID);
            int leftovers = session.Roster.Count;
            var keptId = session.Roster[0].AthleteID;
            int research = session.State.Research;

            var result = session.AdvanceRound();
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, session.Competition.RoundIndex);
            Assert.AreEqual(0, session.Competition.SubmittedScore);
            Assert.AreEqual(research + 5, session.State.Research);
            Assert.AreEqual(leftovers + config.Roster.NewAthletesPerRound, session.Roster.Count);
            Assert.IsNotNull(session.GetAthlete(keptId));
            Assert.AreEqual(1, session.State.Stats.RoundsCompleted);
        }

        [Test]
        public void Recruitment_RespectsRosterCapacity()
        {
            var config = TestHelpers.Config(500, 500, 500, 500);
            config.Roster.NewAthletesPerRound = 3;
            config.Roster.RosterCapacity = 4;
            var session = TestHelpers.NewSession(config);
            int lost = -1;
            session.Events.Subscribe<RoundStartedEvent>(e => lost = e.RecruitsLostToCapacity);

            session.Submit(session.Roster[0].AthleteID);
            session.AdvanceRound(); // 2 kept + 3 new, capped at 4
            Assert.AreEqual(4, session.Roster.Count);
            Assert.AreEqual(1, lost);
        }

        [Test]
        public void GameOver_WhenAllAthletesDestroyedBeforeThreshold()
        {
            var session = TestHelpers.NewSession(TestHelpers.Forced(InjectionOutcome.BodyDestroyed, 100000));
            RunEndedEvent ended = null;
            session.Events.Subscribe<RunEndedEvent>(e => ended = e);
            while (session.Roster.Count > 0) session.Inject(session.Roster[0].AthleteID);
            Assert.AreEqual(RunPhase.GameOver, session.Phase);
            Assert.IsNotNull(ended);
            Assert.IsFalse(ended.Victory);
            Assert.IsFalse(session.AdvanceRound().Success);
        }

        [Test]
        public void NoGameOver_WhenThresholdAlreadyMetAndRosterEmpty()
        {
            var session = TestHelpers.NewSession(TestHelpers.Config(500, 100000));
            session.Submit(session.Roster[0].AthleteID);
            while (session.Roster.Count > 0) session.Sacrifice(session.Roster[0].AthleteID);
            Assert.AreEqual(RunPhase.InProgress, session.Phase);
            Assert.IsTrue(session.AdvanceRound().Success);
            Assert.Greater(session.Roster.Count, 0);
        }

        [Test]
        public void SacrificingLastAthlete_EndsRun_AndIsFlagged()
        {
            var config = TestHelpers.Config(100000);
            config.Roster.NewAthletesPerRound = 1;
            var session = TestHelpers.NewSession(config);
            var athlete = TestHelpers.First(session);
            Assert.IsTrue(session.SacrificeWouldEndRun(athlete));
            session.Sacrifice(athlete.AthleteID);
            Assert.AreEqual(RunPhase.GameOver, session.Phase);
        }

        [Test]
        public void SubmittingLastAthleteShortOfThreshold_IsFlagged_AndEndsRun()
        {
            var config = TestHelpers.Config(100000);
            config.Roster.NewAthletesPerRound = 2;
            var session = TestHelpers.NewSession(config);
            Assert.IsFalse(session.SubmitWouldEndRun(session.Roster[0]), "another athlete is still available");
            session.Submit(session.Roster[0].AthleteID);
            Assert.IsTrue(session.SubmitWouldEndRun(session.Roster[0]));
            session.Submit(session.Roster[0].AthleteID);
            Assert.AreEqual(RunPhase.GameOver, session.Phase);
        }

        [Test]
        public void CompletingFinalRound_WinsTheRun()
        {
            var session = TestHelpers.NewSession(TestHelpers.Config(500, 500));
            session.Submit(session.Roster[0].AthleteID);
            session.AdvanceRound();
            session.Submit(session.Roster[0].AthleteID);
            var result = session.AdvanceRound();
            Assert.IsTrue(result.Success);
            Assert.AreEqual(RunPhase.Victory, session.Phase);
            Assert.IsFalse(session.CanInject(session.SelectedAthlete));
        }

        [Test]
        public void SelectNextAthlete_CyclesThroughActiveAthletes()
        {
            var session = TestHelpers.NewSession(TestHelpers.Config(100000));
            int first = session.State.SelectedAthleteId;
            Assert.IsTrue(session.SelectNextAthlete().Success);
            Assert.AreNotEqual(first, session.State.SelectedAthleteId);
            for (int i = 1; i < session.Roster.Count; i++) session.SelectNextAthlete();
            Assert.AreEqual(first, session.State.SelectedAthleteId);
        }

        [Test]
        public void SameSeedAndActions_ProduceIdenticalRuns()
        {
            Assert.AreEqual(PlayScripted(1234), PlayScripted(1234));
            Assert.AreNotEqual(PlayScripted(1234), PlayScripted(4321));
        }

        [Test]
        public void ResumeFromCapturedState_ContinuesIdentically()
        {
            var config = TestHelpers.Config(3000, 6000, 12000);
            var original = TestHelpers.NewSession(config, 77);
            for (int i = 0; i < 3; i++) original.Inject(original.State.SelectedAthleteId);

            var saved = TestHelpers.DeepCopy(original.CaptureState());
            var resumed = GameSession.Resume(config, RelicCatalog.CreateDefault(), saved);
            Assert.AreEqual(TestHelpers.Fingerprint(original), TestHelpers.Fingerprint(resumed));

            for (int i = 0; i < 10 && !original.IsRunOver; i++)
            {
                original.Inject(original.State.SelectedAthleteId);
                resumed.Inject(resumed.State.SelectedAthleteId);
            }
            Assert.AreEqual(TestHelpers.Fingerprint(original), TestHelpers.Fingerprint(resumed));
        }

        [Test]
        public void InvalidConfig_IsRejected()
        {
            var config = TestHelpers.Config(1000);
            config.Competitions.Clear();
            Assert.Throws<System.ArgumentException>(() => TestHelpers.NewSession(config));
            Assert.IsNotEmpty(config.Validate());
            Assert.IsEmpty(GameConfig.CreateDefault().Validate());
            Assert.IsEmpty(GameConfig.CreateSpecBaseline().Validate());
        }

        [Test]
        public void EventHandlerCannotStartNestedInjection()
        {
            var session = TestHelpers.NewSession(TestHelpers.Forced(InjectionOutcome.Growth, 100000));
            ActionResult nested = null;
            session.Events.Subscribe<InjectionResolvedEvent>(e =>
            {
                if (nested == null) nested = session.Inject(e.Athlete.AthleteID);
            });
            session.Inject(session.State.SelectedAthleteId);
            Assert.IsNotNull(nested);
            Assert.IsFalse(nested.Success);
            Assert.AreEqual(1, session.State.Stats.Injections);
        }

        [Test]
        public void SpecBaseline_KeepsDesignBriefValues()
        {
            var config = GameConfig.CreateSpecBaseline();
            long[] expected = { 2000, 5000, 10000, 20000, 40000, 80000, 150000 };
            Assert.AreEqual(expected.Length, config.Competitions.Count);
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], config.Competitions[i].RequiredScore);
            Assert.AreEqual(3, config.Roster.NewAthletesPerRound);
            Assert.AreEqual(8, config.Rush.ActivationThreshold);
            Assert.AreEqual(3, config.Rush.DurationInjections);
        }

        private static string PlayScripted(long seed)
        {
            var session = TestHelpers.NewSession(TestHelpers.Config(2000, 5000, 10000), seed);
            for (int step = 0; step < 200 && !session.IsRunOver; step++)
            {
                var athlete = session.SelectedAthlete;
                if (session.CanAdvance) session.AdvanceRound();
                else if (athlete.MassLevel < 4) session.Inject(athlete.AthleteID);
                else session.Submit(athlete.AthleteID);
            }
            return TestHelpers.Fingerprint(session);
        }
    }
}
