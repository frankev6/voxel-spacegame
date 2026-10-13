using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class RemoveBodyEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public RemoveBodyEffect() { }
        internal RemoveBodyEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("remove_body", binder);
        public void Apply(ILogicContext context)
        {
            if (!CanApply(context))
            { Game.Core.GameLog.ErrorOnce("NAR.Effect.BodyScope.remove_body", "Narrative", "remove_body requires a body scope; effect skipped."); return; }
            if (!arguments.CanRoute("remove_body")) return;
            context.World.RemoveBody(context.Scope);
        }
        public bool CanApply(ILogicContext context) => context.Scope.HasBody;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
