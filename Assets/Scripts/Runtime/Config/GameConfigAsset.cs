using MassFactory.Core;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>ScriptableObject wrapper so all tuning values can be edited in the Inspector.</summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Mass Factory/Game Config")]
    public sealed class GameConfigAsset : ScriptableObject
    {
        public enum Preset
        {
            Tuned,
            SpecBaseline
        }

        [Tooltip("Values applied when the asset is created or reset from the context menu.")]
        public Preset preset = Preset.Tuned;

        public GameConfig config = GameConfig.CreateDefault();

        private void Reset()
        {
            ApplyPreset();
        }

        [ContextMenu("Apply Preset")]
        public void ApplyPreset()
        {
            config = preset == Preset.SpecBaseline ? GameConfig.CreateSpecBaseline() : GameConfig.CreateDefault();
        }

        /// <summary>Returns a validated copy so runtime changes never write back into the asset.</summary>
        public GameConfig BuildConfig()
        {
            if (config == null || config.Validate().Count > 0)
            {
                Debug.LogWarning("[MassFactory] GameConfig asset is invalid; falling back to defaults. " +
                                 (config == null ? "" : string.Join(" ", config.Validate().ToArray())));
                return GameConfig.CreateDefault();
            }
            return config.Clone();
        }
    }
}
