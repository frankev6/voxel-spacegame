using System;
using System.Collections.Generic;

namespace Game.Data.Logic
{
    /// <summary>Reusable summed costs; nested lists keep independent preflight snapshots.</summary>
    public sealed class EffectRequirements
    {
        readonly struct DeferredCost
        {
            internal readonly IEffect Effect;
            internal readonly int Item;
            internal readonly IIntExpr Amount;
            internal DeferredCost(IEffect effect, int item, IIntExpr amount)
            { Effect = effect; Item = item; Amount = amount; }
        }
        sealed class Frame
        {
            internal readonly Dictionary<int, long> Items = new(), EvaluatedItems = new();
            internal readonly Dictionary<IEffect, int> Prepared = new();
            internal readonly List<DeferredCost> Deferred = new();
            internal long Credits;
            internal bool Overflow;
            internal void Clear()
            { Items.Clear(); EvaluatedItems.Clear(); Prepared.Clear(); Deferred.Clear(); Credits = 0; Overflow = false; }
        }
        readonly List<Frame> frames = new() { new Frame() };
        int depth;
        Frame Current => frames[depth];
        internal void Push()
        {
            depth++;
            if (depth == frames.Count) frames.Add(new Frame());
            Current.Clear();
        }
        internal void Pop() { Current.Clear(); depth--; }
        public void Clear() => Current.Clear();
        public void AddItemCost(int itemIndex, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            AddItem(Current.Items, itemIndex, count);
        }
        static void AddItem(Dictionary<int, long> into, int item, int count)
        {
            if (count == 0) return;
            into.TryGetValue(item, out long previous);
            into[item] = Math.Min((long)int.MaxValue + 1, previous + count);
        }
        public void AddCreditCost(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var frame = Current;
            if (amount > long.MaxValue - frame.Credits) frame.Overflow = true;
            else frame.Credits += amount;
        }
        internal void AddDeferredCost(IEffect effect, int itemIndex, IIntExpr amount)
            => Current.Deferred.Add(new DeferredCost(effect, itemIndex, amount));
        internal int PreparedCost(IEffect effect) => Current.Prepared[effect];
        public bool IsSatisfied(ILogicContext ctx)
        {
            var frame = Current;
            if (frame.Overflow) return false;
            frame.EvaluatedItems.Clear(); frame.Prepared.Clear();
            foreach (var cost in frame.Items) frame.EvaluatedItems.Add(cost.Key, cost.Value);
            long credits = frame.Credits;
            foreach (var cost in frame.Deferred)
            {
                int amount = cost.Amount.Evaluate(ctx);
                if (amount < 1) return false;
                frame.Prepared[cost.Effect] = amount;
                if (cost.Item >= 0) AddItem(frame.EvaluatedItems, cost.Item, amount);
                else
                {
                    if (amount > long.MaxValue - credits) return false;
                    credits += amount;
                }
            }
            if (credits != 0 && ctx.Economy.GetCredits() < credits) return false;
            foreach (var cost in frame.EvaluatedItems)
                if (ctx.Economy.GetItemCount(cost.Key) < cost.Value) return false;
            return true;
        }
    }
}
