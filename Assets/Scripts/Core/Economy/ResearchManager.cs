using System;

namespace MassFactory.Core
{
    /// <summary>Research currency: earned by sacrificing athletes and winning competitions, spent on relics.</summary>
    public sealed class ResearchManager
    {
        private readonly GameSession _session;

        internal ResearchManager(GameSession session)
        {
            _session = session;
        }

        public int Balance => _session.State.Research;

        /// <summary>ResearchGained = Floor(CurrentScore / ScorePerResearch), then relic modifiers.</summary>
        public int PreviewSacrifice(AthleteData athlete)
        {
            if (athlete == null) return 0;
            double perResearch = _session.Config.Economy.ScorePerResearch;
            int baseResearch = (int)Math.Min(int.MaxValue, Math.Floor(athlete.CurrentScore / perResearch));
            return _session.Relics.ModifySacrificeResearch(athlete, baseResearch);
        }

        public void Grant(int amount, string reason)
        {
            if (amount <= 0) return;
            int old = _session.State.Research;
            _session.State.Research = (int)Math.Min(int.MaxValue, (long)old + amount);
            _session.State.Stats.ResearchEarned += amount;
            _session.Events.Publish(new ResearchChangedEvent { OldValue = old, NewValue = _session.State.Research, Reason = reason });
        }

        public bool Spend(int amount, string reason)
        {
            if (amount < 0 || amount > _session.State.Research) return false;
            int old = _session.State.Research;
            _session.State.Research -= amount;
            _session.Events.Publish(new ResearchChangedEvent { OldValue = old, NewValue = _session.State.Research, Reason = reason });
            return true;
        }
    }
}
