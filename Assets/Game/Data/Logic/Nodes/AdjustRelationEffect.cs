using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class AdjustRelationEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public AdjustRelationEffect() { }
        internal AdjustRelationEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("adjust_relation", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("adjust_relation")) return;
            int faction = arguments.Text[0] == "owner" ? context.World.CurrentStarSystemOwnerFactionIndex : arguments.Indices[0];
            context.World.AdjustRelation(faction, arguments.Integers[1].Evaluate(context));
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
