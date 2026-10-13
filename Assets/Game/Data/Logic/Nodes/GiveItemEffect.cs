using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class GiveItemEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public GiveItemEffect() { }
        internal GiveItemEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("give_item", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("give_item")) return;
            context.Economy.GiveItems(arguments.Indices[0], arguments.Integers[1].Evaluate(context));
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
