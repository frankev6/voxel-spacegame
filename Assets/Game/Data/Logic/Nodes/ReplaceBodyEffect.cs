using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class ReplaceBodyEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public ReplaceBodyEffect() { }
        internal ReplaceBodyEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("replace_body", binder);
        public void Apply(ILogicContext context)
        {
            if (!CanApply(context))
            { Game.Core.GameLog.ErrorOnce("NAR.Effect.BodyScope.replace_body", "Narrative", "replace_body requires a body scope; effect skipped."); return; }
            if (!arguments.CanRoute("replace_body")) return;
            context.World.ReplaceBody(context.Scope, arguments.Indices[0]);
        }
        public bool CanApply(ILogicContext context) => context.Scope.HasBody;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
