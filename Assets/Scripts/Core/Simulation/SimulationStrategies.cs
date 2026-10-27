using System;

namespace MassFactory.Core.Simulation
{
    /// <summary>A bot that plays a run by issuing GameSession commands. Each Step performs at most one action.</summary>
    public interface ISimulationStrategy
    {
        string Name { get; }
        string Description { get; }

        /// <summary>Performs one action. Returns false if it could not find anything to do.</summary>
        bool Step(GameSession session);
    }

    /// <summary>
    /// Shared turn structure: shop when affordable, advance once the threshold is met, use the rush when it
    /// matters, otherwise keep working on the selected athlete until the strategy says stop, then submit.
    /// </summary>
    public abstract class StrategyBase : ISimulationStrategy
    {
        public abstract string Name { get; }
        public abstract string Description { get; }

        /// <summary>Buy relics when research allows.</summary>
        public bool UseShop = true;

        /// <summary>Once the threshold is met, sacrifice everyone except this many athletes before advancing.</summary>
        public int SacrificeSurplusKeep = -1;

        protected abstract bool ShouldKeepInjecting(GameSession session, AthleteData athlete);

        public bool Step(GameSession session)
        {
            if (session.IsRunOver) return false;

            if (UseShop && TryShop(session)) return true;

            if (session.Competition.ThresholdMet)
            {
                if (SacrificeSurplusKeep >= 0 && session.ActiveAthleteCount > SacrificeSurplusKeep)
                {
                    var weakest = FindAthlete(session, false);
                    if (weakest != null && session.Sacrifice(weakest.AthleteID).Success) return true;
                }
                return session.AdvanceRound().Success;
            }

            var athlete = FindAthlete(session, true);
            if (athlete == null) return false;
            if (session.State.SelectedAthleteId != athlete.AthleteID) session.SelectAthlete(athlete.AthleteID);

            if (ShouldKeepInjecting(session, athlete))
            {
                if (session.Rush.CanActivate && session.Injection.BuildOutcomeTable(athlete)[InjectionOutcome.BodyDestroyed] >= 12)
                    return session.ActivateRush().Success;
                return session.Inject(athlete.AthleteID).Success;
            }
            return session.Submit(athlete.AthleteID).Success;
        }

        /// <summary>Highest (or lowest) MassLevel active athlete; ties go to roster order.</summary>
        protected static AthleteData FindAthlete(GameSession session, bool highest)
        {
            AthleteData best = null;
            foreach (var athlete in session.Roster)
            {
                if (!athlete.IsActive) continue;
                if (best == null || (highest ? athlete.MassLevel > best.MassLevel : athlete.MassLevel < best.MassLevel))
                    best = athlete;
            }
            return best;
        }

        /// <summary>Relic ids in the order a bot prefers them. Unknown relics rank last.</summary>
        public static readonly string[] RelicPreference =
        {
            "olympia_coach", "forbidden_formula", "fake_muscle_oil", "golden_syringe", "black_market_fridge",
            "mutant_genetics", "second_heart", "underground_laboratory", "no_pain_no_gain", "human_recycling_program",
            "overclocked_adrenals", "adrenaline_pump", "corporate_sponsor", "cheap_needles"
        };

        private static bool TryShop(GameSession session)
        {
            var offer = session.Shop.CurrentOffer();
            int bestIndex = -1;
            int bestRank = int.MaxValue;
            for (int i = 0; i < offer.Count; i++)
            {
                if (session.Shop.WhyCannotBuy(i) != null) continue;
                int rank = Array.IndexOf(RelicPreference, offer[i].Id);
                if (rank < 0) rank = RelicPreference.Length;
                if (rank < bestRank)
                {
                    bestRank = rank;
                    bestIndex = i;
                }
            }
            return bestIndex >= 0 && session.BuyRelic(bestIndex).Success;
        }
    }

    /// <summary>Stops once the next injection's destruction chance reaches a risk threshold.</summary>
    public sealed class RiskThresholdStrategy : StrategyBase
    {
        private readonly string _name;
        public double MaxDestroyChance;

        public RiskThresholdStrategy(string name, double maxDestroyChance)
        {
            _name = name;
            MaxDestroyChance = maxDestroyChance;
        }

        public override string Name => SacrificeSurplusKeep >= 0 ? _name + "+Sacrifice" : _name;
        public override string Description => "Inject while next destroy chance < " + MaxDestroyChance.ToString("0.#") + "%" +
                                              (SacrificeSurplusKeep >= 0 ? ", sacrifice surplus once the threshold is met (keep " + SacrificeSurplusKeep + ")" : "");

        protected override bool ShouldKeepInjecting(GameSession session, AthleteData athlete)
        {
            return session.PreviewDestroyChance(athlete) < MaxDestroyChance;
        }

        public static RiskThresholdStrategy Conservative() { return new RiskThresholdStrategy("Conservative", 15); }
        public static RiskThresholdStrategy Aggressive() { return new RiskThresholdStrategy("Aggressive", 35); }
        public static RiskThresholdStrategy Balanced() { return new RiskThresholdStrategy("Balanced", 25); }
    }

    /// <summary>
    /// Splits the remaining requirement across the active athletes and injects each one until it can carry its share
    /// (with a safety cap on destruction chance).
    /// </summary>
    public sealed class ScoreTargetStrategy : StrategyBase
    {
        public double SafetyCap = 50;
        public double Overshoot = 1.0;

        public override string Name => SacrificeSurplusKeep >= 0 ? "ScoreTarget+Sacrifice" : "ScoreTarget";
        public override string Description => "Inject until score >= remaining requirement / active athletes" +
                                              (SacrificeSurplusKeep >= 0 ? ", then sacrifice surplus (keep " + SacrificeSurplusKeep + ")" : "");

        protected override bool ShouldKeepInjecting(GameSession session, AthleteData athlete)
        {
            if (session.PreviewDestroyChance(athlete) >= SafetyCap) return false;
            int active = Math.Max(1, session.ActiveAthleteCount);
            double share = (double)session.Competition.RemainingScore / active * Overshoot;
            return athlete.CurrentScore < share;
        }
    }
}
