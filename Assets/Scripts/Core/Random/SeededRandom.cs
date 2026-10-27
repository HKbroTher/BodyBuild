using System;

namespace MassFactory.Core
{
    public interface IRandomSource
    {
        /// <summary>Uniform double in [0, 1).</summary>
        double NextDouble();

        /// <summary>Uniform int in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }

    /// <summary>
    /// Small deterministic xorshift64* generator. Identical results on .NET and Unity/Mono,
    /// and its whole state is a single 64-bit value, which makes save/resume and seeded tests trivial.
    /// </summary>
    public sealed class SeededRandom : IRandomSource
    {
        private ulong _state;

        public SeededRandom(ulong seed)
        {
            _state = SplitMix64(seed);
            if (_state == 0) _state = 0x9E3779B97F4A7C15UL;
        }

        private SeededRandom() { }

        public static SeededRandom FromState(ulong state)
        {
            return new SeededRandom { _state = state == 0 ? 0x9E3779B97F4A7C15UL : state };
        }

        public ulong State => _state;

        public double NextDouble()
        {
            return (NextULong() >> 11) * (1.0 / (1UL << 53));
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            int value = (int)(NextDouble() * maxExclusive);
            return value >= maxExclusive ? maxExclusive - 1 : value;
        }

        private ulong NextULong()
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        private static ulong SplitMix64(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
