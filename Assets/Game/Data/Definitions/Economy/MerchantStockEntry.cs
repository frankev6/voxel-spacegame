namespace Game.Data.Definitions
{
    /// <summary>SPEC-50 §4.1: count -1 is unlimited; priceFactor 0 is read as 1.</summary>
    [System.Serializable] public struct MerchantStockEntry
    {
        public ItemDefinition item;
        public int count;
        public float priceFactor;
    }
}
