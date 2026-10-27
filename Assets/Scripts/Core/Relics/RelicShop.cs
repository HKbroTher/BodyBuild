using System.Collections.Generic;

namespace MassFactory.Core
{
    /// <summary>
    /// One offering of random relic choices per round. The player may buy one choice per offering,
    /// and can pay research to restock a fresh offering.
    /// </summary>
    public sealed class RelicShop
    {
        private readonly GameSession _session;

        internal RelicShop(GameSession session)
        {
            _session = session;
        }

        private ShopState State => _session.State.Shop;

        public IReadOnlyList<string> OfferIds => State.OfferIds;
        public bool OfferPurchased => State.Purchased;

        public List<RelicDefinition> CurrentOffer()
        {
            var list = new List<RelicDefinition>();
            foreach (var id in State.OfferIds)
            {
                var def = _session.Catalog.Get(id);
                if (def != null) list.Add(def);
            }
            return list;
        }

        public int PriceOf(RelicDefinition relic)
        {
            return _session.Relics.ApplyCostModifiers(relic.Cost);
        }

        public int RestockPrice()
        {
            var economy = _session.Config.Economy;
            return _session.Relics.ApplyCostModifiers(economy.RestockBaseCost + economy.RestockCostIncrease * State.RestockCount);
        }

        public bool RelicSlotsFull => _session.Relics.Count >= _session.Config.Economy.MaxRelics;

        public string WhyCannotBuy(int choiceIndex)
        {
            if (_session.State.Phase != RunPhase.InProgress) return "The run is over.";
            if (State.Purchased) return "Already bought from this offering.";
            if (choiceIndex < 0 || choiceIndex >= State.OfferIds.Count) return "Invalid choice.";
            var relic = _session.Catalog.Get(State.OfferIds[choiceIndex]);
            if (relic == null) return "Unknown relic.";
            if (_session.Relics.Has(relic.Id)) return "Already equipped.";
            if (RelicSlotsFull) return "All relic slots are full.";
            if (_session.State.Research < PriceOf(relic)) return "Not enough research.";
            return null;
        }

        public string WhyCannotRestock()
        {
            if (_session.State.Phase != RunPhase.InProgress) return "The run is over.";
            if (_session.State.Research < RestockPrice()) return "Not enough research.";
            if (BuildCandidates().Count == 0) return "Nothing left to stock.";
            return null;
        }

        internal ActionResult<RelicDefinition> Buy(int choiceIndex)
        {
            string error = WhyCannotBuy(choiceIndex);
            if (error != null) return ActionResult<RelicDefinition>.Fail(error);

            var relic = _session.Catalog.Get(State.OfferIds[choiceIndex]);
            int price = PriceOf(relic);
            _session.Research.Spend(price, "Bought " + relic.DisplayName);
            State.Purchased = true;
            _session.Relics.Add(relic);
            _session.Events.Publish(new RelicPurchasedEvent { Relic = relic, Cost = price });
            return ActionResult<RelicDefinition>.Ok(relic);
        }

        internal ActionResult Restock()
        {
            string error = WhyCannotRestock();
            if (error != null) return ActionResult.Fail(error);
            _session.Research.Spend(RestockPrice(), "Shop restock");
            State.RestockCount++;
            GenerateOffer();
            return ActionResult.Ok();
        }

        /// <summary>Called at the start of every round. Restock prices reset with it.</summary>
        internal void StartRound()
        {
            State.RestockCount = 0;
            GenerateOffer();
        }

        internal void GenerateOffer()
        {
            var candidates = BuildCandidates();
            State.OfferIds.Clear();
            State.Purchased = false;
            int choices = _session.Config.Economy.ShopChoices;
            while (State.OfferIds.Count < choices && candidates.Count > 0)
            {
                int pick = _session.Rng.Next(candidates.Count);
                State.OfferIds.Add(candidates[pick].Id);
                candidates.RemoveAt(pick);
            }
        }

        private List<RelicDefinition> BuildCandidates()
        {
            var candidates = new List<RelicDefinition>();
            foreach (var relic in _session.Catalog.All)
            {
                if (relic.AvailableInShop && !_session.Relics.Has(relic.Id)) candidates.Add(relic);
            }
            return candidates;
        }
    }
}
