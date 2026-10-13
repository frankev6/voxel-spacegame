using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class GiveLootEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        [NonSerialized] int sysBeatsCompletedSlot = -1;
        public GiveLootEffect() { }
        internal GiveLootEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder)
        {
            arguments.Bind("give_loot", binder);
            sysBeatsCompletedSlot = binder.BindFact("sys_beats_completed");
        }
        public void Apply(ILogicContext context)
        {
            if (!arguments.CanRoute("give_loot") || sysBeatsCompletedSlot < 0) return;
            ulong scope = ((ulong)(uint)context.Scope.BeatIndex << 32) | (uint)context.Scope.QuestIndex;
            ulong salt = Game.Core.StableHash.Combine(scope, (ulong)(uint)context.Facts.GetBySlot(sysBeatsCompletedSlot));
            context.Economy.GiveLoot(arguments.Indices[0], arguments.Integers[1].Evaluate(context), salt);
        }
        public bool CanApply(ILogicContext context) => true;
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
