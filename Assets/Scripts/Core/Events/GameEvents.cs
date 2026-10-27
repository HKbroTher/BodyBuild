namespace MassFactory.Core
{
    // Gameplay notifications published on GameEventBus. Relics react to them through
    // RelicManager; the presentation layer uses them to drive feedback.

    public sealed class RunStartedEvent { }

    public sealed class RoundStartedEvent
    {
        public int RoundIndex;
        public string CompetitionName;
        public long RequiredScore;
        public int Recruited;
        public int RecruitsLostToCapacity;
    }

    public sealed class AthleteRecruitedEvent
    {
        public AthleteData Athlete;
        public bool IsBonusRecruit;
    }

    public sealed class BeforeInjectionEvent
    {
        public AthleteData Athlete;
    }

    public sealed class InjectionResolvedEvent
    {
        public AthleteData Athlete;
        public InjectionResult Result;
    }

    public sealed class MassLevelChangedEvent
    {
        public AthleteData Athlete;
        public int OldLevel;
        public int NewLevel;
    }

    public sealed class AthleteDestroyedEvent
    {
        public AthleteData Athlete;
        public int LevelAtDestruction;
    }

    public sealed class AthleteSubmittedEvent
    {
        public AthleteData Athlete;
        public long Score;
    }

    public sealed class AthleteSacrificedEvent
    {
        public AthleteData Athlete;
        public int ResearchGained;
    }

    public sealed class CompetitionCompletedEvent
    {
        public int RoundIndex;
        public string CompetitionName;
        public long RequiredScore;
        public long SubmittedScore;
        public long ExcessScore;
        public int ResearchReward;
    }

    public sealed class ResearchChangedEvent
    {
        public int OldValue;
        public int NewValue;
        public string Reason;
    }

    public sealed class RelicPurchasedEvent
    {
        public RelicDefinition Relic;
        public int Cost;
    }

    public sealed class RelicTriggeredEvent
    {
        public RelicDefinition Relic;
        public string Message;
    }

    public sealed class RelicConsumedEvent
    {
        public RelicDefinition Relic;
    }

    public sealed class RushActivatedEvent
    {
        public int Duration;
    }

    public sealed class RushEndedEvent { }

    public sealed class RunEndedEvent
    {
        public bool Victory;
        public string Reason;
    }

    /// <summary>Published after every successful command so UIs can refresh.</summary>
    public sealed class StateChangedEvent { }
}
