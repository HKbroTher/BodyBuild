using System.Collections.Generic;
using MassFactory.Core;
using MassFactory.Core.Simulation;
using NUnit.Framework;

namespace MassFactory.Tests
{
    public class RushAndShopTests
    {
        [Test]
        public void Rush_ChargesFromSuccesses_AndRemovesDestruction()
        {
            var config = TestHelpers.Forced(InjectionOutcome.Growth, 10_000_000);
            config.Rush.ActivationThreshold = 3;
            config.Rush.DurationInjections = 2;
            var session = TestHelpers.NewSession(config);
            var athlete = TestHelpers.First(session);

            Assert.IsFalse(session.Rush.CanActivate);
            for (int i = 0; i < 3; i++) session.Inject(athlete.AthleteID);
            Assert.AreEqual(3, session.Rush.Energy);
            Assert.IsTrue(session.Rush.CanActivate);

            TestHelpers.Force(session.Config, InjectionOutcome.BodyDestroyed);
            Assert.IsTrue(session.ActivateRush().Success);
            Assert.IsTrue(session.Rush.IsActive);
            Assert.AreEqual(0, session.Rush.Energy);
            Assert.AreEqual(0.0, session.PreviewDestroyChance(athlete), 1e-9);

            var first = session.Inject(athlete.AthleteID).Value;
            Assert.IsTrue(first.DuringRush);
            Assert.IsFalse(first.AthleteDestroyed);
            Assert.AreEqual(100.0, first.Probabilities.Total, 1e-9);
            var second = session.Inject(athlete.AthleteID).Value;
            Assert.IsTrue(second.RushEnded);
            Assert.IsFalse(session.Rush.IsActive);

            Assert.IsTrue(session.Inject(athlete.AthleteID).Value.AthleteDestroyed, "rush is over");
        }

        [Test]
        public void Rush_DoesNotChargeDuringRush_OrOnFailures()
        {
            var config = TestHelpers.Forced(InjectionOutcome.Mutation, 10_000_000);
            config.Rush.ActivationThreshold = 2;
            config.Rush.DurationInjections = 2;
            var session = TestHelpers.NewSession(config);
            var athlete = TestHelpers.First(session);
            session.Inject(athlete.AthleteID);
            Assert.AreEqual(2, session.Rush.Energy, "mutation gives 2 energy");
            session.ActivateRush();
            session.Inject(athlete.AthleteID);
            Assert.AreEqual(0, session.Rush.Energy);
            Assert.IsFalse(session.ActivateRush().Success, "cannot re-activate while active");

            TestHelpers.Force(session.Config, InjectionOutcome.NoEffect);
            session.Inject(athlete.AthleteID);
            session.Inject(athlete.AthleteID);
            Assert.AreEqual(0, session.Rush.Energy);
        }

        [Test]
        public void Shop_OffersUniqueChoices_AndAllowsOnePurchase()
        {
            var config = TestHelpers.Config(100000);
            config.Economy.StartingResearch = 100;
            var session = TestHelpers.NewSession(config);
            var offer = session.Shop.CurrentOffer();
            Assert.AreEqual(3, offer.Count);
            Assert.AreEqual(3, new HashSet<string>(session.Shop.OfferIds).Count);

            int price = session.Shop.PriceOf(offer[1]);
            var bought = session.BuyRelic(1);
            Assert.IsTrue(bought.Success);
            Assert.AreEqual(100 - price, session.State.Research);
            Assert.IsTrue(session.Relics.Has(offer[1].Id));
            Assert.IsFalse(session.BuyRelic(0).Success, "only one purchase per offering");
        }

        [Test]
        public void Shop_RejectsPurchaseWithoutResearch()
        {
            var session = TestHelpers.NewSession(TestHelpers.Config(100000));
            Assert.AreEqual("Not enough research.", session.Shop.WhyCannotBuy(0));
            Assert.IsFalse(session.BuyRelic(0).Success);
            Assert.AreEqual(0, session.Relics.Count);
        }

        [Test]
        public void Shop_Restock_ChargesAndRefreshes()
        {
            var config = TestHelpers.Config(100000);
            config.Economy.StartingResearch = 100;
            var session = TestHelpers.NewSession(config);
            session.BuyRelic(0);
            int research = session.State.Research;
            int price = session.Shop.RestockPrice();
            Assert.IsTrue(session.RestockShop().Success);
            Assert.AreEqual(research - price, session.State.Research);
            Assert.IsFalse(session.Shop.OfferPurchased);
            Assert.Greater(session.Shop.RestockPrice(), price, "restocks get more expensive");
        }

        [Test]
        public void Shop_RespectsMaxRelicSlots()
        {
            var config = TestHelpers.Config(100000);
            config.Economy.StartingResearch = 1000;
            config.Economy.MaxRelics = 1;
            var session = TestHelpers.NewSession(config);
            Assert.IsTrue(session.BuyRelic(0).Success);
            session.RestockShop();
            Assert.AreEqual("All relic slots are full.", session.Shop.WhyCannotBuy(0));
        }

        [Test]
        public void Shop_NeverOffersEquippedRelics()
        {
            var config = TestHelpers.Config(100000);
            config.Economy.StartingResearch = 1000;
            var session = TestHelpers.NewSession(config);
            for (int i = 0; i < 6; i++)
            {
                session.BuyRelic(0);
                session.RestockShop();
                foreach (var id in session.Shop.OfferIds) Assert.IsFalse(session.Relics.Has(id), id);
            }
        }

        [Test]
        public void Simulation_RunsWithoutStallsOrLoops()
        {
            var runner = new SimulationRunner(GameConfig.CreateDefault(), RelicCatalog.CreateDefault());
            var strategies = new System.Func<ISimulationStrategy>[]
            {
                () => RiskThresholdStrategy.Conservative(),
                () => RiskThresholdStrategy.Aggressive(),
                () => new ScoreTargetStrategy(),
                () => new RiskThresholdStrategy("Conservative", 15) { SacrificeSurplusKeep = 1 }
            };
            foreach (var factory in strategies)
            {
                var report = runner.Run(factory, 200, 1);
                Assert.AreEqual(0, report.StalledRuns, report.StrategyName);
                Assert.AreEqual(0, report.ActionCapHits, report.StrategyName);
                Assert.AreEqual(200, report.Victories + SumValues(report.FailedAtRound), report.StrategyName);
            }
        }

        [Test]
        public void Simulation_IsReproducible()
        {
            var runner = new SimulationRunner(GameConfig.CreateDefault(), RelicCatalog.CreateDefault());
            var a = runner.Run(() => new ScoreTargetStrategy(), 100, 5);
            var b = runner.Run(() => new ScoreTargetStrategy(), 100, 5);
            Assert.AreEqual(a.TotalInjections, b.TotalInjections);
            Assert.AreEqual(a.Victories, b.Victories);
            Assert.AreEqual(a.TotalSubmittedScore, b.TotalSubmittedScore);
        }

        private static int SumValues(Dictionary<int, int> histogram)
        {
            int sum = 0;
            foreach (var v in histogram.Values) sum += v;
            return sum;
        }
    }
}
