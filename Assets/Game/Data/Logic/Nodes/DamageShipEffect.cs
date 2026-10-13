using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class DamageShipEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public DamageShipEffect() { }
        internal DamageShipEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("damage_ship", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("damage_ship")) return;
            context.World.DamagePlayerShip(arguments.Integers[0].Evaluate(context), context.Scope);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
