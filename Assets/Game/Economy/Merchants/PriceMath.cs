namespace Game.Economy
{
    /// <summary>SPEC-50 contract shell; ECO-005 owns behavior.</summary>
    public static class PriceMath    // §5.4; pure
    {
        public static int BuyPrice(int basePrice, float entryFactor, float merchantSellFactor, int relation, float slope) => throw new System.NotImplementedException("ECO-005");   // ≥ 1
        public static int SellPrice(int basePrice, float merchantBuyFactor, int relation, float slope) => throw new System.NotImplementedException("ECO-005");                    // ≥ 0
    }
}
