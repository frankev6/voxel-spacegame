using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class AbortBeatEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public AbortBeatEffect() { }
        internal AbortBeatEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("abort_beat", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("abort_beat")) return;
            context.Narrative.AbortCurrentBeat();
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
