using System;
using System.Text;

namespace MassFactory.Core
{
    /// <summary>
    /// Weighted, mutually exclusive outcome distribution for a single injection.
    /// Values are percentages; call <see cref="Normalize"/> to make them sum to exactly 100.
    /// </summary>
    public sealed class OutcomeTable
    {
        /// <summary>Roll order. Destruction first so the cumulative ranges stay stable while tuning.</summary>
        private static readonly InjectionOutcome[] RollOrder =
        {
            InjectionOutcome.BodyDestroyed, InjectionOutcome.Atrophy, InjectionOutcome.NoEffect,
            InjectionOutcome.Growth, InjectionOutcome.Mutation
        };

        private readonly double[] _weights = new double[InjectionOutcomeExtensions.Count];

        public double this[InjectionOutcome outcome]
        {
            get => _weights[(int)outcome];
            set => _weights[(int)outcome] = (double.IsNaN(value) || value < 0) ? 0 : value;
        }

        public double Total
        {
            get
            {
                double total = 0;
                for (int i = 0; i < _weights.Length; i++) total += _weights[i];
                return total;
            }
        }

        public OutcomeTable Clone()
        {
            var copy = new OutcomeTable();
            Array.Copy(_weights, copy._weights, _weights.Length);
            return copy;
        }

        /// <summary>Scales all weights to sum to exactly 100. An empty table becomes 100% NO EFFECT.</summary>
        public void Normalize()
        {
            double total = Total;
            if (total <= 0)
            {
                Array.Clear(_weights, 0, _weights.Length);
                this[InjectionOutcome.NoEffect] = 100;
                return;
            }

            int last = -1;
            double sum = 0;
            for (int i = 0; i < _weights.Length; i++)
            {
                _weights[i] = _weights[i] * 100.0 / total;
                if (_weights[i] > 0) last = i;
            }
            for (int i = 0; i < _weights.Length; i++)
                if (i != last) sum += _weights[i];
            _weights[last] = Math.Max(0, 100.0 - sum);
        }

        /// <summary>
        /// Moves up to <paramref name="amount"/> percentage points into <paramref name="target"/>,
        /// draining the sources in the given order. Returns the amount actually moved.
        /// </summary>
        public double Shift(InjectionOutcome target, double amount, params InjectionOutcome[] sources)
        {
            if (amount <= 0) return 0;
            double moved = 0;
            foreach (var source in sources)
            {
                if (source == target) continue;
                double take = Math.Min(this[source], amount - moved);
                if (take <= 0) continue;
                this[source] -= take;
                moved += take;
                if (moved >= amount) break;
            }
            this[target] += moved;
            return moved;
        }

        /// <summary>Picks an outcome for a uniform roll in [0, 1).</summary>
        public InjectionOutcome Roll(double roll01)
        {
            double total = Total;
            if (total <= 0) return InjectionOutcome.NoEffect;
            double point = roll01 * total;
            double cumulative = 0;
            InjectionOutcome lastNonZero = InjectionOutcome.NoEffect;
            foreach (var outcome in RollOrder)
            {
                double weight = this[outcome];
                if (weight <= 0) continue;
                lastNonZero = outcome;
                cumulative += weight;
                if (point < cumulative) return outcome;
            }
            return lastNonZero;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var outcome in RollOrder)
                sb.Append(outcome.ToLabel()).Append('=').Append(this[outcome].ToString("0.##")).Append("% ");
            return sb.ToString().TrimEnd();
        }
    }
}
