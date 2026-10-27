using System;

namespace MassFactory.Core
{
    public static class ScoreCalculator
    {
        /// <summary>
        /// Score = BaseScore × LevelMultiplier ^ MassLevel × personal multiplier × global multiplier,
        /// rounded half away from zero and clamped to <see cref="GameConfig.MaxScore"/>.
        /// </summary>
        public static long Calculate(double baseScore, double levelMultiplier, int massLevel,
            double personalMultiplier = 1.0, double globalMultiplier = 1.0)
        {
            if (massLevel < 0) massLevel = 0;
            double raw = baseScore * Math.Pow(levelMultiplier, massLevel) * personalMultiplier * globalMultiplier;
            if (double.IsNaN(raw) || raw <= 0) return 0;
            if (double.IsInfinity(raw) || raw >= GameConfig.MaxScore) return GameConfig.MaxScore;
            return (long)Math.Round(raw, MidpointRounding.AwayFromZero);
        }

        public static long SaturatingAdd(long a, long b)
        {
            long result = a + b;
            if (result > GameConfig.MaxScore || (a > 0 && b > 0 && result < 0)) return GameConfig.MaxScore;
            return result;
        }
    }
}
