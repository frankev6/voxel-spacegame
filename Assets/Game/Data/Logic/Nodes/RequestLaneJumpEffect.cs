using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class RequestLaneJumpEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        public RequestLaneJumpEffect() { }
        internal RequestLaneJumpEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Terminal;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("request_lane_jump", binder);
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("request_lane_jump")) return;
            context.World.RequestLaneJump(arguments.Indices[0]);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
