using System;

namespace MassFactory.Core
{
    /// <summary>
    /// Resolves injections: base table → relic modifiers → rush → normalize → roll →
    /// relic transforms → apply. Each call resolves exactly one outcome.
    /// </summary>
    public sealed class InjectionSystem
    {
        private readonly GameSession _session;

        internal InjectionSystem(GameSession session)
        {
            _session = session;
        }

        /// <summary>Final probabilities the next injection on this athlete would use. Safe to call for previews.</summary>
        public OutcomeTable BuildOutcomeTable(AthleteData athlete)
        {
            var table = InjectionProbabilityCalculator.CalculateBase(_session.Config.Injection, athlete.MassLevel);
            _session.Relics.ModifyOutcomeTable(athlete, table);
            _session.Rush.ModifyOutcomeTable(table);
            table.Normalize();
            return table;
        }

        internal InjectionResult Resolve(AthleteData athlete)
        {
            var state = _session.State;
            var config = _session.Config.Injection;

            _session.Events.Publish(new BeforeInjectionEvent { Athlete = athlete });

            bool duringRush = _session.Rush.IsActive;
            var table = BuildOutcomeTable(athlete);
            var rolled = table.Roll(_session.Rng.NextDouble());
            var outcome = _session.Relics.TransformOutcome(athlete, rolled, out string transformedBy);

            var result = new InjectionResult
            {
                AthleteId = athlete.AthleteID,
                RolledOutcome = rolled,
                Outcome = outcome,
                LevelBefore = athlete.MassLevel,
                ScoreBefore = athlete.CurrentScore,
                DestroyChance = table[InjectionOutcome.BodyDestroyed],
                Probabilities = table,
                DuringRush = duringRush,
                TransformedBy = transformedBy
            };

            athlete.InjectionCount++;
            athlete.InjectionsThisRound++;
            state.Stats.Injections++;

            switch (outcome)
            {
                case InjectionOutcome.Mutation:
                    athlete.MutationCount++;
                    athlete.ConsecutiveAtrophy = 0;
                    state.Stats.Mutations++;
                    _session.AddMassLevels(athlete, config.MutationLevels);
                    break;
                case InjectionOutcome.Growth:
                    athlete.ConsecutiveAtrophy = 0;
                    state.Stats.Growths++;
                    _session.AddMassLevels(athlete, config.GrowthLevels);
                    break;
                case InjectionOutcome.NoEffect:
                    athlete.ConsecutiveAtrophy = 0;
                    state.Stats.NoEffects++;
                    break;
                case InjectionOutcome.Atrophy:
                    athlete.ConsecutiveAtrophy++;
                    state.Stats.Atrophies++;
                    _session.AddMassLevels(athlete, -config.AtrophyLevels);
                    break;
                case InjectionOutcome.BodyDestroyed:
                    athlete.IsAlive = false;
                    state.Stats.Destroyed++;
                    state.Roster.Remove(athlete);
                    break;
                default:
                    throw new InvalidOperationException("Unhandled outcome " + outcome);
            }

            result.LevelAfter = athlete.MassLevel;
            result.ScoreAfter = athlete.IsAlive ? athlete.CurrentScore : 0;
            result.RushEnded = _session.Rush.OnInjectionResolved(outcome, duringRush);

            _session.Events.Publish(new InjectionResolvedEvent { Athlete = athlete, Result = result });

            if (outcome == InjectionOutcome.BodyDestroyed)
                _session.Events.Publish(new AthleteDestroyedEvent { Athlete = athlete, LevelAtDestruction = result.LevelBefore });

            return result;
        }
    }
}
