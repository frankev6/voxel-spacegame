using System;

namespace Game.Economy
{
    /// <summary>SPEC-50 §5.4: relation-adjusted integer prices, with explicit rounding and representable bounds.</summary>
    public static class PriceMath
    {
        public static int BuyPrice(int basePrice, float entryFactor, float merchantSellFactor, int relation, float slope)
        {
            Check(entryFactor, nameof(entryFactor)); Check(merchantSellFactor, nameof(merchantSellFactor)); Check(slope, nameof(slope));
            float factor = 1 - slope * Math.Clamp(relation, -50, 50);
            if (basePrice <= 0 || merchantSellFactor == 0 || factor <= 0) return 1;
            // Use the authored float expression from the spec before rounding; widen only for the checked integer conversion.
            float price = basePrice * (entryFactor == 0 ? 1 : entryFactor) * merchantSellFactor * factor;
            return Bound(Math.Round(price, MidpointRounding.AwayFromZero), 1);
        }
        public static int SellPrice(int basePrice, float merchantBuyFactor, int relation, float slope)
        {
            Check(merchantBuyFactor, nameof(merchantBuyFactor)); Check(slope, nameof(slope));
            float factor = 1 + slope * Math.Clamp(relation, -50, 50);
            if (basePrice <= 0 || merchantBuyFactor == 0 || factor <= 0) return 0;
            return Bound(Math.Floor(basePrice * merchantBuyFactor * factor), 0);
        }
        static int Bound(double price, int minimum) => double.IsNaN(price) || price >= int.MaxValue ? int.MaxValue : price <= minimum ? minimum : (int)price;
        static void Check(float value, string parameter)
        { if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException(parameter); }
    }
}
