using System;

namespace MassFactory.Core
{
    /// <summary>
    /// ANABOLIC RUSH: successful injections charge energy; once full, the player can activate a
    /// rush during which BODY DESTROYED is removed from the outcome pool for a few injections.
    /// Energy is not gained while a rush is running, so rushes cannot chain into themselves.
    /// </summary>
    public sealed class RushManager
    {
        private readonly GameSession _session;

        internal RushManager(GameSession session)
        {
            _session = session;
        }

        private RushConfig Config => _session.Config.Rush;

        public bool Enabled => Config.Enabled;
        public bool IsActive => _session.State.RushRemaining > 0;
        public int Energy => _session.State.RushEnergy;
        public int RemainingInjections => _session.State.RushRemaining;

        public int Threshold => Math.Max(Math.Max(1, Config.MinActivationThreshold),
            _session.Relics.ModifyRushThreshold(Config.ActivationThreshold));

        public int Duration => _session.Relics.ModifyRushDuration(Config.DurationInjections);

        public bool CanActivate => Enabled && _session.State.Phase == RunPhase.InProgress && !IsActive && Energy >= Threshold;

        internal bool Activate()
        {
            if (!CanActivate) return false;
            _session.State.RushEnergy = 0;
            _session.State.RushRemaining = Duration;
            _session.State.Stats.RushesActivated++;
            _session.Events.Publish(new RushActivatedEvent { Duration = _session.State.RushRemaining });
            return true;
        }

        /// <summary>Removes destruction from the table while a rush is active.</summary>
        internal void ModifyOutcomeTable(OutcomeTable table)
        {
            if (IsActive) table[InjectionOutcome.BodyDestroyed] = 0;
        }

        /// <summary>Called once per resolved injection. Returns true when this injection ended the rush.</summary>
        internal bool OnInjectionResolved(InjectionOutcome outcome, bool duringRush)
        {
            if (!Enabled) return false;
            if (duringRush)
            {
                _session.State.RushRemaining = Math.Max(0, _session.State.RushRemaining - 1);
                if (_session.State.RushRemaining == 0)
                {
                    _session.Events.Publish(new RushEndedEvent());
                    return true;
                }
                return false;
            }

            int gain = outcome == InjectionOutcome.Mutation ? Config.EnergyPerMutation
                : outcome == InjectionOutcome.Growth ? Config.EnergyPerGrowth : 0;
            if (gain > 0)
                _session.State.RushEnergy = Math.Min(Threshold, _session.State.RushEnergy + gain);
            return false;
        }
    }
}
