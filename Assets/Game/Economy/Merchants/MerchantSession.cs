namespace Game.Economy
{
    /// <summary>SPEC-50 contract shell; ECO-005 owns behavior.</summary>
    public sealed class MerchantSession
    {
        public Game.Data.Definitions.MerchantDefinition Merchant => throw new System.NotImplementedException("ECO-005");
        public bool Refused => throw new System.NotImplementedException("ECO-005");                    // hostile owner: no trades
        public string RefusalReason => throw new System.NotImplementedException("ECO-005");            // "" unless Refused
        public int Relation => throw new System.NotImplementedException("ECO-005");
        public System.Collections.Generic.IReadOnlyList<MerchantStockState> Stock => throw new System.NotImplementedException("ECO-005");
        public int BuyPriceOf(int stockIndex) => throw new System.NotImplementedException("ECO-005");
        public int SellPriceOf(Game.Data.Definitions.ItemDefinition item) => throw new System.NotImplementedException("ECO-005");  // 0 when the merchant does not buy it
        public bool CanBuy(int stockIndex, int quantity, out string reason) => throw new System.NotImplementedException("ECO-005");
        public bool TryBuy(int stockIndex, int quantity, out string reason) => throw new System.NotImplementedException("ECO-005");
        public bool CanSell(int slot, int quantity, out string reason) => throw new System.NotImplementedException("ECO-005");
        public bool TrySell(int slot, int quantity, out string reason) => throw new System.NotImplementedException("ECO-005");
        public event System.Action Changed { add => throw new System.NotImplementedException("ECO-005"); remove => throw new System.NotImplementedException("ECO-005"); }             // stock, wallet or cargo changed through this session
        public void Close() => throw new System.NotImplementedException("ECO-005");                            // idempotent
    }
}
