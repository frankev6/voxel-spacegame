using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class GiveLootEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public GiveLootEffect() { }
        internal GiveLootEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("give_loot", binder);
        // TODO(NAR-005): implement effect execution, preflight and summed requirements.
        public void Apply(ILogicContext context) => throw new NotImplementedException("NAR-005");
        public bool CanApply(ILogicContext context) => throw new NotImplementedException("NAR-005");
        public void AddRequirements(EffectRequirements requirements) => throw new NotImplementedException("NAR-005");
    }
}
