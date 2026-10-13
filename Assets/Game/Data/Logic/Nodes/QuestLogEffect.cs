using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class QuestLogEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public QuestLogEffect() { }
        internal QuestLogEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("quest_log", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("quest_log")) return;
            context.Narrative.AddQuestLog(arguments.Indices[0], arguments.Text[1]);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
