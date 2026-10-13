namespace Game.Economy
{
    /// <summary>SPEC-50 §4.4: one mutable stock row, owned by MerchantService.</summary>
    public sealed class MerchantStockState
    {
        public Game.Data.Definitions.ItemDefinition Item;
        public int Count;                 // −1 unlimited
        public float PriceFactor;
        public bool Buyback;              // added by player sales; removed at restock
        public Game.Core.ContentId OriginalId;   // Missing placeholder support
    }
}
