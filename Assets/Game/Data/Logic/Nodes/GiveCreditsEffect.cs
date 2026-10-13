using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class GiveCreditsEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public GiveCreditsEffect() { }
        internal GiveCreditsEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("give_credits", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("give_credits")) return;
            context.Economy.GiveCredits(arguments.Integers[0].Evaluate(context));
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
