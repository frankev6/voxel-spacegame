using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class QueueBeatEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public QueueBeatEffect() { }
        internal QueueBeatEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("queue_beat", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("queue_beat")) return;
            context.Narrative.QueueBeat(arguments.Indices[0]);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
