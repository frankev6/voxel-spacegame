using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class StartCombatEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public StartCombatEffect() { }
        internal StartCombatEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Terminal;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("start_combat", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("start_combat")) return;
            context.World.StartCombat(arguments.Indices[0], context.Scope);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
