using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class OpenShipEditorEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public OpenShipEditorEffect() { }
        internal OpenShipEditorEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Terminal;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("open_ship_editor", binder);
        // TODO(NAR-005): implement effect execution, preflight and summed requirements.
        public void Apply(ILogicContext context) => throw new NotImplementedException("NAR-005");
        public bool CanApply(ILogicContext context) => throw new NotImplementedException("NAR-005");
        public void AddRequirements(EffectRequirements requirements) => throw new NotImplementedException("NAR-005");
    }
}
