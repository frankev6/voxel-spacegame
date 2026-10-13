using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class RepairShipEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public RepairShipEffect() { }
        internal RepairShipEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("repair_ship", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("repair_ship")) return;
            context.World.RepairPlayerShip(arguments.Integers[0].Evaluate(context));
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
