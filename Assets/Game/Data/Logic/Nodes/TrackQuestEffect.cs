using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class TrackQuestEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public TrackQuestEffect() { }
        internal TrackQuestEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("track_quest", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("track_quest")) return;
            context.Narrative.TrackQuest(arguments.Indices[0]);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
