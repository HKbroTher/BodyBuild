using UnityEngine;

namespace MassFactory.Presentation
{
    public enum MuscleGroup
    {
        Chest = 0,
        Shoulders = 1,
        Biceps = 2,
        Triceps = 3,
        Back = 4,
        Quadriceps = 5,
        Calves = 6,
        OverallMuscleMass = 7
    }

    /// <summary>Anything that can display per-muscle growth: the primitive placeholder or a blend shape mesh.</summary>
    public interface IMuscleRig
    {
        /// <summary>amount: 0 = baseline human, ~1 = pro bodybuilder, 2-3 = freak, 4+ = mutant.</summary>
        void SetMuscle(MuscleGroup group, float amount);
        void SetSkinTint(Color color);
    }

    /// <summary>Maps MassLevel to per-group growth using the tiers from the design brief.</summary>
    public static class MuscleProfile
    {
        public const int GroupCount = 8;

        private static readonly float[] GroupWeights = { 1.0f, 1.15f, 1.2f, 1.0f, 1.05f, 0.9f, 0.7f, 0.6f };

        /// <summary>Piecewise growth: gentle at 0–3, pro at 4–7, extreme at 8–11, absurd at 12+.</summary>
        public static float MassAmount(float level)
        {
            if (level <= 0) return 0;
            if (level <= 3) return level * 0.12f;
            if (level <= 7) return 0.36f + (level - 3) * 0.2f;
            if (level <= 11) return 1.16f + (level - 7) * 0.3f;
            return Mathf.Min(8f, 2.36f + (level - 11) * 0.45f);
        }

        public static void Evaluate(float level, float[] output)
        {
            float mass = MassAmount(level);
            for (int i = 0; i < GroupCount; i++) output[i] = mass * GroupWeights[i];
        }

        public static string TierName(int level)
        {
            if (level <= 3) return "Gym Regular";
            if (level <= 7) return "Pro Physique";
            if (level <= 11) return "Freak";
            return "MUTANT";
        }

        public static Color SkinTint(Color baseSkin, float level)
        {
            // Pumped skin goes red, mutants turn an unhealthy purple-green.
            var pumped = Color.Lerp(baseSkin, new Color(0.85f, 0.35f, 0.3f), Mathf.Clamp01(level / 11f) * 0.45f);
            if (level < 12) return pumped;
            return Color.Lerp(pumped, new Color(0.45f, 0.55f, 0.35f), Mathf.Clamp01((level - 11) / 6f) * 0.6f);
        }
    }
}
