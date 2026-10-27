using System.Collections.Generic;
using MassFactory.Core;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>The list of relics available in a run. Empty catalogs fall back to the built-in defaults.</summary>
    [CreateAssetMenu(fileName = "RelicCatalog", menuName = "Mass Factory/Relic Catalog")]
    public sealed class RelicCatalogAsset : ScriptableObject
    {
        public List<RelicDefinitionAsset> relics = new List<RelicDefinitionAsset>();

        public RelicCatalog BuildCatalog()
        {
            var definitions = new List<RelicDefinition>();
            foreach (var asset in relics)
            {
                if (asset == null || asset.definition == null || string.IsNullOrEmpty(asset.definition.Id)) continue;
                var d = asset.definition;
                // Copy so tweaks made at runtime (tests, debug) never dirty the asset.
                definitions.Add(new RelicDefinition(d.Id, d.DisplayName, d.EffectType, d.Cost, d.Value, d.Value2,
                    d.Description, d.FlavorText, d.Consumable) { AvailableInShop = d.AvailableInShop });
            }
            return definitions.Count > 0 ? new RelicCatalog(definitions) : RelicCatalog.CreateDefault();
        }
    }
}
