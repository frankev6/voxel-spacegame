using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class GiveShipEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public GiveShipEffect() { }
        internal GiveShipEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("give_ship", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("give_ship")) return;
            context.World.GiveShip(arguments.Indices[0]);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
