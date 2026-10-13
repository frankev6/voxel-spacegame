using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class RevealClueEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public RevealClueEffect() { }
        internal RevealClueEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("reveal_clue", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("reveal_clue")) return;
            context.Narrative.RevealClue(arguments.Indices[0]);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
