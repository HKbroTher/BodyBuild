using System;
using System.Collections.Generic;

namespace MassFactory.Core
{
    /// <summary>
    /// Owns the equipped relics of a run. Runs modifier pipelines on request and forwards
    /// gameplay events from the session's <see cref="GameEventBus"/> to each relic's hooks.
    /// </summary>
    public sealed class RelicManager
    {
        private sealed class Entry
        {
            public RelicDefinition Definition;
            public RelicInstance Instance;
            public RelicEffect Effect;
            public RelicContext Context;
            public int Order;
        }

        private readonly GameSession _session;
        private readonly List<Entry> _entries = new List<Entry>();
        private int _nextOrder;

        internal RelicManager(GameSession session)
        {
            _session = session;
            foreach (var instance in session.State.Relics)
            {
                var definition = session.Catalog.Get(instance.RelicId);
                if (definition != null) AddEntry(definition, instance);
            }

            var bus = session.Events;
            bus.Subscribe<RoundStartedEvent>(e => ForEach(x => x.Effect.OnRoundStarted(x.Context, e)));
            bus.Subscribe<AthleteRecruitedEvent>(e => ForEach(x => x.Effect.OnAthleteRecruited(x.Context, e.Athlete)));
            bus.Subscribe<BeforeInjectionEvent>(e => ForEach(x => x.Effect.BeforeInjection(x.Context, e.Athlete)));
            bus.Subscribe<InjectionResolvedEvent>(e => ForEach(x => x.Effect.AfterOutcomeResolved(x.Context, e.Athlete, e.Result)));
            bus.Subscribe<MassLevelChangedEvent>(e => ForEach(x => x.Effect.OnMassLevelChanged(x.Context, e.Athlete, e.OldLevel, e.NewLevel)));
            bus.Subscribe<AthleteDestroyedEvent>(e => ForEach(x => x.Effect.OnAthleteDestroyed(x.Context, e.Athlete)));
            bus.Subscribe<AthleteSacrificedEvent>(e => ForEach(x => x.Effect.OnAthleteSacrificed(x.Context, e.Athlete, e.ResearchGained)));
            bus.Subscribe<AthleteSubmittedEvent>(e => ForEach(x => x.Effect.OnAthleteSubmitted(x.Context, e.Athlete, e.Score)));
            bus.Subscribe<CompetitionCompletedEvent>(e => ForEach(x => x.Effect.OnCompetitionCompleted(x.Context, e)));
        }

        public int Count => _entries.Count;

        public IEnumerable<RelicDefinition> Equipped
        {
            get
            {
                foreach (var entry in _entries) yield return entry.Definition;
            }
        }

        public bool Has(string relicId)
        {
            foreach (var entry in _entries)
                if (entry.Definition.Id == relicId) return true;
            return false;
        }

        /// <summary>Equips a relic. Gameplay code should go through GameSession.BuyRelic; this is also used by tests/sim.</summary>
        public bool Add(RelicDefinition definition)
        {
            if (definition == null || Has(definition.Id)) return false;
            var instance = new RelicInstance { RelicId = definition.Id };
            _session.State.Relics.Add(instance);
            var entry = AddEntry(definition, instance);
            entry.Effect.OnAcquired(entry.Context);
            _session.RefreshAllScores();
            return true;
        }

        /// <summary>Removes a consumable relic after it fired.</summary>
        public void Consume(RelicInstance instance)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Instance != instance) continue;
                var definition = _entries[i].Definition;
                _entries.RemoveAt(i);
                _session.State.Relics.Remove(instance);
                _session.Events.Publish(new RelicConsumedEvent { Relic = definition });
                return;
            }
        }

        // ---- Modifier pipelines ----------------------------------------------------------------

        public void ModifyOutcomeTable(AthleteData athlete, OutcomeTable table)
        {
            var sorted = Snapshot();
            sorted.Sort((a, b) => a.Effect.ProbabilityPriority != b.Effect.ProbabilityPriority
                ? a.Effect.ProbabilityPriority.CompareTo(b.Effect.ProbabilityPriority)
                : a.Order.CompareTo(b.Order));
            foreach (var entry in sorted)
                entry.Effect.ModifyOutcomeTable(entry.Context, athlete, table);
        }

        public InjectionOutcome TransformOutcome(AthleteData athlete, InjectionOutcome outcome, out string transformedBy)
        {
            transformedBy = null;
            foreach (var entry in Snapshot())
            {
                var next = entry.Effect.TransformOutcome(entry.Context, athlete, outcome);
                if (next != outcome)
                {
                    transformedBy = entry.Definition.DisplayName;
                    outcome = next;
                }
            }
            return outcome;
        }

        public bool PreventsNextDestruction(AthleteData athlete)
        {
            foreach (var entry in _entries)
                if (entry.Effect.PreventsNextDestruction(entry.Context, athlete)) return true;
            return false;
        }

        public double GlobalScoreMultiplier()
        {
            double multiplier = 1.0;
            foreach (var entry in _entries)
                multiplier = entry.Effect.ModifyGlobalScoreMultiplier(entry.Context, multiplier);
            return Math.Max(0, multiplier);
        }

        public int ModifyRecruitCount(int count)
        {
            foreach (var entry in _entries) count = entry.Effect.ModifyRecruitCount(entry.Context, count);
            return Math.Max(0, count);
        }

        public int ModifySacrificeResearch(AthleteData athlete, int research)
        {
            foreach (var entry in _entries) research = entry.Effect.ModifySacrificeResearch(entry.Context, athlete, research);
            return Math.Max(0, research);
        }

        public int ApplyCostModifiers(int baseCost)
        {
            double multiplier = 1.0;
            foreach (var entry in _entries) multiplier = entry.Effect.ModifyCostMultiplier(entry.Context, multiplier);
            return Math.Max(0, (int)Math.Ceiling(baseCost * Math.Max(0, multiplier) - 1e-9));
        }

        public int ModifyRushThreshold(int threshold)
        {
            foreach (var entry in _entries) threshold = entry.Effect.ModifyRushThreshold(entry.Context, threshold);
            return threshold;
        }

        public int ModifyRushDuration(int duration)
        {
            foreach (var entry in _entries) duration = entry.Effect.ModifyRushDuration(entry.Context, duration);
            return Math.Max(1, duration);
        }

        // ---- Internals --------------------------------------------------------------------------

        private Entry AddEntry(RelicDefinition definition, RelicInstance instance)
        {
            var entry = new Entry
            {
                Definition = definition,
                Instance = instance,
                Effect = RelicEffectFactory.Create(definition.EffectType),
                Order = _nextOrder++
            };
            entry.Context = new RelicContext(_session, definition, instance);
            _entries.Add(entry);
            return entry;
        }

        private List<Entry> Snapshot()
        {
            return new List<Entry>(_entries);
        }

        private void ForEach(Action<Entry> action)
        {
            foreach (var entry in Snapshot())
            {
                // Skip relics consumed earlier in the same dispatch.
                if (_entries.Contains(entry)) action(entry);
            }
        }
    }
}
