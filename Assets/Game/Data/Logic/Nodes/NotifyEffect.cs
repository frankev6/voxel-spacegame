using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class NotifyEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public NotifyEffect() { }
        internal NotifyEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("notify", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("notify")) return;
            context.World.Notify(arguments.Text[0]);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
