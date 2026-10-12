using System;
using System.Globalization;
using Game.Core;

namespace Game.UI.Data
{
    /// <summary>SPEC-60 §5.6: invariant number strings, immutable after construction.</summary>
    public static class NumberStringCache
    {
        [StaticStateOk("Private table populated only by the static constructor; never exposed or mutated afterwards.")]
        static readonly string[] positive = new string[10000];
        [StaticStateOk("Private table populated only by the static constructor; never exposed or mutated afterwards.")]
        static readonly string[] negative = new string[9999];
        [StaticStateOk("Private table populated only by the static constructor; never exposed or mutated afterwards.")]
        static readonly string[] twoDigits = new string[100];

        static NumberStringCache()
        {
            for (int i = 0; i < positive.Length; i++) positive[i] = i.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < negative.Length; i++) negative[i] = (-i - 1).ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < twoDigits.Length; i++) twoDigits[i] = i.ToString("D2", CultureInfo.InvariantCulture);
        }

        public static string Get(int value)
        {
            if (value >= 0 && value < positive.Length) return positive[value];
            if (value < 0 && value >= -negative.Length) return negative[-value - 1];
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Formats a clock field in [0, 99]; rejects values outside that contract.</summary>
        public static string Get2Digits(int value)
        {
            if (value < 0 || value >= twoDigits.Length) throw new ArgumentOutOfRangeException(nameof(value));
            return twoDigits[value];
        }
    }
}
