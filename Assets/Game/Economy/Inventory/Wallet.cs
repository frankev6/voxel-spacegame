using System;

namespace Game.Economy
{
    /// <summary>SPEC-50 §4.3: bounded credits with saturating grants and atomic spending.</summary>
    public sealed class Wallet
    {
        public const long MaxCredits = 1_000_000_000_000L;
        public long Credits { get; private set; }
        public event Action Changed;

        public void Add(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            // Compare before adding so even long.MaxValue cannot overflow.
            Set(amount >= MaxCredits - Credits ? MaxCredits : Credits + amount);
        }

        public bool TrySpend(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Credits) return false;
            Set(Credits - amount); return true;
        }

        public void Set(long credits)
        {
            long next = Math.Clamp(credits, 0, MaxCredits);
            if (next == Credits) return;
            Credits = next; Changed?.Invoke();
        }
    }
}
