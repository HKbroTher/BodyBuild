using System;
using System.Collections.Generic;

namespace MassFactory.Core
{
    /// <summary>Behaviour implemented by a relic. Each value maps to one RelicEffect class.</summary>
    public enum RelicEffectType
    {
        GoldenSyringe = 0,
        BlackMarketFridge = 1,
        OlympiaCoach = 2,
        MutantGenetics = 3,
        FakeMuscleOil = 4,
        UndergroundLaboratory = 5,
        HumanRecyclingProgram = 6,
        CorporateSponsor = 7,
        ForbiddenFormula = 8,
        SecondHeart = 9,
        CheapNeedles = 10,
        NoPainNoGain = 11,
        AdrenalinePump = 12,
        OverclockedAdrenals = 13
    }

    /// <summary>
    /// Static relic data. Lives inside a Unity ScriptableObject (RelicDefinitionAsset) or is
    /// created in code by <see cref="RelicCatalog.CreateDefault"/>. Value/Value2 meaning depends on the effect type.
    /// </summary>
    [Serializable]
    public class RelicDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public string FlavorText;
        public int Cost;
        public RelicEffectType EffectType;
        public double Value;
        public double Value2;
        public bool Consumable;
        public bool AvailableInShop = true;

        public RelicDefinition() { }

        public RelicDefinition(string id, string name, RelicEffectType type, int cost, double value, double value2,
            string description, string flavor, bool consumable = false)
        {
            Id = id;
            DisplayName = name;
            EffectType = type;
            Cost = cost;
            Value = value;
            Value2 = value2;
            Description = description;
            FlavorText = flavor;
            Consumable = consumable;
        }
    }

    /// <summary>The set of relics that can appear in a run.</summary>
    public sealed class RelicCatalog
    {
        private readonly List<RelicDefinition> _relics = new List<RelicDefinition>();
        private readonly Dictionary<string, RelicDefinition> _byId = new Dictionary<string, RelicDefinition>();

        public IReadOnlyList<RelicDefinition> All => _relics;

        public RelicCatalog(IEnumerable<RelicDefinition> relics)
        {
            foreach (var relic in relics)
            {
                if (relic == null || string.IsNullOrEmpty(relic.Id) || _byId.ContainsKey(relic.Id)) continue;
                _relics.Add(relic);
                _byId[relic.Id] = relic;
            }
        }

        public RelicDefinition Get(string id)
        {
            return id != null && _byId.TryGetValue(id, out var relic) ? relic : null;
        }

        public static RelicCatalog CreateDefault()
        {
            return new RelicCatalog(CreateDefaultDefinitions());
        }

        public static List<RelicDefinition> CreateDefaultDefinitions()
        {
            return new List<RelicDefinition>
            {
                new RelicDefinition("golden_syringe", "Golden Syringe", RelicEffectType.GoldenSyringe, 8, 6, 0,
                    "+6% MUTATION chance (taken from GROWTH).",
                    "Gold-plated. Still single use. Still reused."),
                new RelicDefinition("black_market_fridge", "Black Market Fridge", RelicEffectType.BlackMarketFridge, 9, 0, 0,
                    "Each athlete's first injection every round cannot destroy them.",
                    "Keeps the product cold and the questions colder."),
                new RelicDefinition("olympia_coach", "Olympia Coach", RelicEffectType.OlympiaCoach, 10, 1, 0,
                    "Recruited athletes start with +1 MassLevel.",
                    "Yells \"LIGHT WEIGHT\" at things that are not weights."),
                new RelicDefinition("mutant_genetics", "Mutant Genetics", RelicEffectType.MutantGenetics, 8, 50, 0,
                    "ATROPHY has a 50% chance to become GROWTH.",
                    "Family tree has more branches than usual."),
                new RelicDefinition("fake_muscle_oil", "Fake Muscle Oil", RelicEffectType.FakeMuscleOil, 9, 1.5, 4,
                    "Competition score x1.5. +4% BODY DESTROYED chance.",
                    "Judges cannot tell the difference. Neither can the liver."),
                new RelicDefinition("underground_laboratory", "Underground Laboratory", RelicEffectType.UndergroundLaboratory, 7, 1, 5,
                    "Each destroyed athlete gives the next recruit +1 MassLevel (stacks up to +5).",
                    "Nothing is wasted. Everything is documented. Nothing is legal."),
                new RelicDefinition("human_recycling_program", "Human Recycling Program", RelicEffectType.HumanRecyclingProgram, 7, 3, 0,
                    "Every 3 destroyed athletes recruit 1 extra athlete immediately.",
                    "Some assembly required."),
                new RelicDefinition("corporate_sponsor", "Corporate Sponsor", RelicEffectType.CorporateSponsor, 6, 1, 1000,
                    "Each 1,000 excess competition score earns +1 research when a competition is completed.",
                    "Brought to you by a beverage that is technically a beverage."),
                new RelicDefinition("forbidden_formula", "Forbidden Formula", RelicEffectType.ForbiddenFormula, 10, 1.25, 0,
                    "Each MUTATION permanently multiplies that athlete's score by x1.25.",
                    "The label is just a skull. A happy skull."),
                new RelicDefinition("second_heart", "Second Heart", RelicEffectType.SecondHeart, 6, 0, 0,
                    "Prevents the next BODY DESTROYED once, then is consumed.",
                    "Installed backstage. Warranty void.", true),
                new RelicDefinition("cheap_needles", "Cheap Needles", RelicEffectType.CheapNeedles, 5, 0.75, 0,
                    "Relic and restock prices are reduced by 25%.",
                    "Bulk discount. Bulk everything."),
                new RelicDefinition("no_pain_no_gain", "No Pain No Gain", RelicEffectType.NoPainNoGain, 6, 8, 5,
                    "Each consecutive ATROPHY moves +8% into GROWTH/MUTATION for that athlete (max 5 stacks).",
                    "Pain is weakness leaving the body. Along with other things."),
                new RelicDefinition("adrenaline_pump", "Adrenaline Pump", RelicEffectType.AdrenalinePump, 6, 2, 0,
                    "ANABOLIC RUSH needs 2 less energy.",
                    "Hand-cranked. Do not crank faster."),
                new RelicDefinition("overclocked_adrenals", "Overclocked Adrenals", RelicEffectType.OverclockedAdrenals, 8, 2, 0,
                    "ANABOLIC RUSH lasts 2 more injections.",
                    "Runs hot. Smells like burnt toast.")
            };
        }
    }
}
