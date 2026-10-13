using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;

namespace Game.Economy
{
    /// <summary>SPEC-50 §5.4: definition-wide stock, open-time restocking and one current trading session.</summary>
    public sealed class MerchantService
    {
        internal readonly InventoryModel Inventory;
        internal readonly Wallet Wallet;
        internal readonly EconomyConfigDefinition Config;
        readonly ContentCatalog catalog;
        readonly Func<double> universalTime;
        readonly Dictionary<ushort, State> states = new Dictionary<ushort, State>();
        bool trading;
        public MerchantSession Current { get; private set; }

        internal sealed class State
        {
            public readonly List<MerchantStockState> Rows = new List<MerchantStockState>();
            public readonly IReadOnlyList<MerchantStockState> View;
            public double LastRestock;
            public State(MerchantDefinition merchant, double now) { View = Rows.AsReadOnly(); Restock(merchant, now); }
            public void Restock(MerchantDefinition merchant, double now)
            {
                // Rebuild in authored order; this also removes buybacks and restores missing/depleted authored rows.
                Rows.Clear();
                foreach (var entry in merchant.Stock)
                    Rows.Add(new MerchantStockState { Item = entry.item, Count = entry.count, PriceFactor = entry.priceFactor });
                LastRestock = now;
            }
        }
        public MerchantService(InventoryModel inventory, Wallet wallet, ContentCatalog catalog,
                               EconomyConfigDefinition config, Func<double> universalTime)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory)); Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Config = config != null ? config : throw new ArgumentNullException(nameof(config));
            this.universalTime = universalTime ?? throw new ArgumentNullException(nameof(universalTime));
        }
        public MerchantSession OpenSession(MerchantDefinition merchant, int relation, bool refuse, string refusalReason)
        {
            CheckIdle(); if (merchant == null) throw new ArgumentNullException(nameof(merchant));
            ushort index = catalog.IndexOf(merchant);
            if (index == 0) throw new ArgumentException("Merchant is not in the content catalog.", nameof(merchant));
            double now = universalTime();
            if (double.IsNaN(now) || double.IsInfinity(now)) throw new InvalidOperationException("Merchant game time must be finite.");
            Current?.Close();
            if (!states.TryGetValue(index, out var state)) states.Add(index, state = new State(merchant, now));
            else if (merchant.RestockHours > 0 && now - state.LastRestock >= (double)merchant.RestockHours * 3600)
                state.Restock(merchant, now);
            return Current = new MerchantSession(this, state, merchant, relation, refuse, refusalReason);
        }
        internal bool BeginTrade(out string reason)
        {
            reason = trading ? "Trade in progress" : null;
            if (trading) return false;
            trading = true; return true;
        }
        internal void EndTrade() => trading = false;
        internal void Closed(MerchantSession session) { if (ReferenceEquals(Current, session)) Current = null; }
        void CheckIdle() { if (trading) throw new InvalidOperationException("Cannot open or reset merchants during a trade."); }
        public void ResetToEmpty() { CheckIdle(); Current?.Close(); states.Clear(); }
        // TODO(ECO-006): the spec leaves INVT restore parameters unspecified; define them with the participant.
        internal void RestoreState() => throw new System.NotImplementedException("ECO-006");
    }
}
