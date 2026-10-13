using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class SpawnEncounterEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public SpawnEncounterEffect() { }
        internal SpawnEncounterEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("spawn_encounter", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("spawn_encounter")) return;
            context.World.SpawnEncounter(arguments.Indices[0], arguments.Text[1], arguments.Integers[2].Evaluate(context), context.Scope);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
