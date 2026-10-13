using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class OpenMerchantEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public OpenMerchantEffect() { }
        internal OpenMerchantEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Blocking;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("open_merchant", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("open_merchant")) return;
            var continuation = (context as ILogicEffectHost)?.BlockingCommandCompleted;
            if (continuation == null)
            {
                Game.Core.GameLog.ErrorOnce("NAR.Effect.MerchantContinuation", "Narrative", "open_merchant requires a dialog continuation; effect skipped.");
                return;
            }
            context.World.OpenMerchant(arguments.Indices[0], context.Scope, continuation);
        }
        public bool CanApply(ILogicContext context) => (context as ILogicEffectHost)?.BlockingCommandCompleted != null;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
