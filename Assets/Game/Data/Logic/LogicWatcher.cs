using System;
using System.Collections.Generic;
using Game.Core.Events;

namespace Game.Data.Logic
{
    /// <summary>Re-evaluates a bound condition on matching dependencies (SPEC-40 §6). Main thread only.</summary>
    public sealed class LogicWatcher : IDisposable
    {
        readonly ICondition condition;
        readonly ILogicContext context;
        readonly Action<bool> onChanged;
        readonly int[] factSlots, itemIndices;
        readonly ILogicEconomy economy;
        readonly Action<FactChanged> factChanged;
        readonly Action<int> itemChanged;
        readonly Action creditsChanged;
        bool disposed;

        public LogicWatcher(ICondition condition, ILogicContext context, Action<bool> onChanged)
        {
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            var dependencies = new List<LogicDependency>();
            condition.CollectDependencies(dependencies);
            factSlots = Keys(dependencies, LogicDependencyKind.Fact);
            itemIndices = Keys(dependencies, LogicDependencyKind.Item);
            bool watchesCredits = false;
            foreach (var dependency in dependencies)
                if (dependency.Kind == LogicDependencyKind.Credits) { watchesCredits = true; break; }
            if (itemIndices.Length != 0 || watchesCredits)
                economy = context.Economy ?? throw new ArgumentException("Economy dependencies require an economy service.", nameof(context));

            // Initial evaluation establishes the value; construction does not report a flip.
            // Evaluate before subscribing so a failed condition cannot leave a live subscription.
            Value = condition.Evaluate(context);
            if (factSlots.Length != 0) { factChanged = OnFactChanged; EventBus<FactChanged>.Subscribe(factChanged); }
            if (itemIndices.Length != 0) { itemChanged = OnItemChanged; economy.ItemCountChanged += itemChanged; }
            if (watchesCredits) { creditsChanged = Refresh; economy.CreditsChanged += creditsChanged; }
        }

        public bool Value { get; private set; }

        /// <summary>Owners refresh location/time and other dependencies without an event on these interfaces.</summary>
        public void Refresh()
        {
            if (disposed) return;
            bool next = condition.Evaluate(context);
            if (next == Value) return;
            Value = next;
            onChanged(next);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (factChanged != null) EventBus<FactChanged>.Unsubscribe(factChanged);
            if (itemChanged != null) economy.ItemCountChanged -= itemChanged;
            if (creditsChanged != null) economy.CreditsChanged -= creditsChanged;
        }

        void OnFactChanged(FactChanged change)
        {
            if (!disposed && Array.BinarySearch(factSlots, change.Slot) >= 0) Refresh();
        }
        void OnItemChanged(int itemIndex)
        {
            if (!disposed && Array.BinarySearch(itemIndices, itemIndex) >= 0) Refresh();
        }

        static int[] Keys(List<LogicDependency> dependencies, LogicDependencyKind kind)
        {
            var keys = new List<int>();
            foreach (var dependency in dependencies) if (dependency.Kind == kind) keys.Add(dependency.Key);
            if (keys.Count == 0) return Array.Empty<int>();
            keys.Sort();
            int count = 1;
            for (int i = 1; i < keys.Count; i++)
                if (keys[i] != keys[count - 1]) keys[count++] = keys[i];
            var result = new int[count];
            keys.CopyTo(0, result, 0, count);
            return result;
        }
    }
}
