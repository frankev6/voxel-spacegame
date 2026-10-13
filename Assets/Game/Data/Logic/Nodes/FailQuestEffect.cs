using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class FailQuestEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public FailQuestEffect() { }
        internal FailQuestEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("fail_quest", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("fail_quest")) return;
            context.Narrative.FailQuest(arguments.Indices[0]);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
