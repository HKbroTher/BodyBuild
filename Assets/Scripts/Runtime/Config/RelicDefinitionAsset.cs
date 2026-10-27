using MassFactory.Core;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>Static definition of one relic, editable in the Inspector.</summary>
    [CreateAssetMenu(fileName = "Relic", menuName = "Mass Factory/Relic Definition")]
    public sealed class RelicDefinitionAsset : ScriptableObject
    {
        public RelicDefinition definition = new RelicDefinition();

        /// <summary>Copies a built-in default into this asset (used by the editor asset generator).</summary>
        public void CopyFrom(RelicDefinition source)
        {
            definition = new RelicDefinition(source.Id, source.DisplayName, source.EffectType, source.Cost, source.Value,
                source.Value2, source.Description, source.FlavorText, source.Consumable);
        }
    }
}
