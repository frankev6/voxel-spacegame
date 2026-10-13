using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class TakeItemEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public TakeItemEffect() { }
        internal TakeItemEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("take_item", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("take_item")) return;
            Spend(context, arguments.Integers[1].Evaluate(context));
        }
        internal void ApplyPrepared(ILogicContext context, EffectRequirements requirements)
        {
            if (!arguments.CanRoute("take_item")) return;
            Spend(context, requirements.PreparedCost(this));
        }
        void Spend(ILogicContext context, int amount)
        {
            if (amount < 1 || !context.Economy.TryTakeItems(arguments.Indices[0], amount))
                Game.Core.GameLog.ErrorOnce("NAR.Effect.Unpaid.take_item", "Narrative", "take_item could not pay its cost; no resources taken.");
        }
        public bool CanApply(ILogicContext context)
        {
            if (arguments.Indices == null || arguments.Indices.Length == 0 || arguments.Indices[0] == 0) return true; // Unresolved references are logged no-ops at execution.
            int amount = arguments.Integers[1].Evaluate(context);
            return amount > 0 && context.Economy.GetItemCount(arguments.Indices[0]) >= amount;
        }
        public void AddRequirements(EffectRequirements requirements)
        {
            if (arguments.Indices == null || arguments.Indices.Length == 0 || arguments.Indices[0] == 0) return;
            requirements.AddDeferredCost(this, arguments.Indices[0], arguments.Integers[1]);
        }
    }
}
