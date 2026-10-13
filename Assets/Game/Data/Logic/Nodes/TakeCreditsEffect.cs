using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class TakeCreditsEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public TakeCreditsEffect() { }
        internal TakeCreditsEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("take_credits", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("take_credits")) return;
            Spend(context, arguments.Integers[0].Evaluate(context));
        }
        internal void ApplyPrepared(ILogicContext context, EffectRequirements requirements)
        {
            if (!arguments.CanRoute("take_credits")) return;
            Spend(context, requirements.PreparedCost(this));
        }
        void Spend(ILogicContext context, int amount)
        {
            if (amount < 1 || !context.Economy.TryTakeCredits(amount))
                Game.Core.GameLog.ErrorOnce("NAR.Effect.Unpaid.take_credits", "Narrative", "take_credits could not pay its cost; no resources taken.");
        }
        public bool CanApply(ILogicContext context)
        {
            if (arguments.Indices == null) return true; // Unresolved references are logged no-ops at execution.
            int amount = arguments.Integers[0].Evaluate(context);
            return amount > 0 && context.Economy.GetCredits() >= amount;
        }
        public void AddRequirements(EffectRequirements requirements)
        {
            if (arguments.Indices == null) return;
            requirements.AddDeferredCost(this, -1, arguments.Integers[0]);
        }
    }
}
