using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class PlaySfxEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public PlaySfxEffect() { }
        internal PlaySfxEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("play_sfx", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("play_sfx")) return;
            context.World.PlaySfx(arguments.Indices[0]);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
