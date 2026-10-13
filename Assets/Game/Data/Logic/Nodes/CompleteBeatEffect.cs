using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class CompleteBeatEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public CompleteBeatEffect() { }
        internal CompleteBeatEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("complete_beat", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("complete_beat")) return;
            context.Narrative.CompleteBeat(arguments.Indices[0]);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
