namespace MassFactory.Core
{
    /// <summary>Immutable record of one resolved injection, used by UI, tests and the simulator.</summary>
    public sealed class InjectionResult
    {
        public int AthleteId;
        /// <summary>Outcome produced by the random roll, before relic transformations.</summary>
        public InjectionOutcome RolledOutcome;
        /// <summary>Outcome that was actually applied.</summary>
        public InjectionOutcome Outcome;
        public int LevelBefore;
        public int LevelAfter;
        public long ScoreBefore;
        public long ScoreAfter;
        public double DestroyChance;
        public OutcomeTable Probabilities;
        public bool DuringRush;
        public bool RushEnded;
        /// <summary>Display name of the relic that changed the rolled outcome, if any.</summary>
        public string TransformedBy;

        public bool AthleteDestroyed => Outcome == InjectionOutcome.BodyDestroyed;
        public int LevelDelta => LevelAfter - LevelBefore;
    }
}
