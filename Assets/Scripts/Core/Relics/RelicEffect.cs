namespace MassFactory.Core
{
    /// <summary>What a relic hook can see: the session, its definition and its own saved counters.</summary>
    public sealed class RelicContext
    {
        public GameSession Session { get; }
        public RelicDefinition Definition { get; }
        public RelicInstance Instance { get; }

        internal RelicContext(GameSession session, RelicDefinition definition, RelicInstance instance)
        {
            Session = session;
            Definition = definition;
            Instance = instance;
        }

        /// <summary>Shows a short "relic triggered" message in the UI.</summary>
        public void Announce(string message)
        {
            Session.Events.Publish(new RelicTriggeredEvent { Relic = Definition, Message = message });
        }
    }

    /// <summary>
    /// Base class for relic behaviour. Effects are stateless; persistent per-relic data lives in
    /// <see cref="RelicInstance"/>. Two kinds of hooks exist:
    ///  - modifier pipelines, queried by the rules before something is resolved (probabilities, costs...);
    ///  - event reactions, invoked by <see cref="RelicManager"/> when gameplay events are published.
    /// New relics only need a new subclass and a factory entry; gameplay code never checks relic ids.
    /// </summary>
    public abstract class RelicEffect
    {
        /// <summary>Order for probability modifiers. Protective effects use a high value so they run last.</summary>
        public virtual int ProbabilityPriority => 0;

        // ---- Modifier pipelines -------------------------------------------------------------

        /// <summary>BeforeOutcomeResolved: adjust the outcome table before the roll.</summary>
        public virtual void ModifyOutcomeTable(RelicContext ctx, AthleteData athlete, OutcomeTable table) { }

        /// <summary>Change the rolled outcome before it is applied. Return the (possibly new) outcome.</summary>
        public virtual InjectionOutcome TransformOutcome(RelicContext ctx, AthleteData athlete, InjectionOutcome outcome)
        {
            return outcome;
        }

        /// <summary>True if this relic will cancel the next destruction (UI hint only).</summary>
        public virtual bool PreventsNextDestruction(RelicContext ctx, AthleteData athlete) { return false; }

        public virtual double ModifyGlobalScoreMultiplier(RelicContext ctx, double multiplier) { return multiplier; }
        public virtual int ModifyRecruitCount(RelicContext ctx, int count) { return count; }
        public virtual int ModifySacrificeResearch(RelicContext ctx, AthleteData athlete, int research) { return research; }
        public virtual double ModifyCostMultiplier(RelicContext ctx, double multiplier) { return multiplier; }
        public virtual int ModifyRushThreshold(RelicContext ctx, int threshold) { return threshold; }
        public virtual int ModifyRushDuration(RelicContext ctx, int duration) { return duration; }

        // ---- Event reactions ----------------------------------------------------------------

        public virtual void OnAcquired(RelicContext ctx) { }
        public virtual void OnRoundStarted(RelicContext ctx, RoundStartedEvent evt) { }
        public virtual void OnAthleteRecruited(RelicContext ctx, AthleteData athlete) { }
        public virtual void BeforeInjection(RelicContext ctx, AthleteData athlete) { }
        public virtual void AfterOutcomeResolved(RelicContext ctx, AthleteData athlete, InjectionResult result) { }
        public virtual void OnMassLevelChanged(RelicContext ctx, AthleteData athlete, int oldLevel, int newLevel) { }
        public virtual void OnAthleteDestroyed(RelicContext ctx, AthleteData athlete) { }
        public virtual void OnAthleteSacrificed(RelicContext ctx, AthleteData athlete, int research) { }
        public virtual void OnAthleteSubmitted(RelicContext ctx, AthleteData athlete, long score) { }
        public virtual void OnCompetitionCompleted(RelicContext ctx, CompetitionCompletedEvent evt) { }
    }
}
