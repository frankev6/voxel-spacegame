using System;
using System.Collections.Generic;

namespace Game.Data.Logic
{
    /// <summary>Reused, summed costs checked before any effect in a list is applied.</summary>
    public sealed class EffectRequirements
    {
        readonly Dictionary<int, long> itemCosts = new();
        long creditCost;
        bool creditOverflow;

        public void Clear()
        {
            itemCosts.Clear();
            creditCost = 0;
            creditOverflow = false;
        }

        public void AddItemCost(int itemIndex, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0) return;
            itemCosts.TryGetValue(itemIndex, out long previous);
            // Held counts are Int32. Retain one value above that domain to reject
            // an impossible cost without ever wrapping a long list's accumulator.
            itemCosts[itemIndex] = Math.Min((long)int.MaxValue + 1, previous + count);
        }

        public void AddCreditCost(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > long.MaxValue - creditCost) creditOverflow = true;
            else creditCost += amount;
        }

        public bool IsSatisfied(ILogicContext ctx)
        {
            if (creditOverflow || (creditCost != 0 && ctx.Economy.GetCredits() < creditCost)) return false;
            foreach (var cost in itemCosts)
                if (ctx.Economy.GetItemCount(cost.Key) < cost.Value) return false;
            return true;
        }
    }
}
