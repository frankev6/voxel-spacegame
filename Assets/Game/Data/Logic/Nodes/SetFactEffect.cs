using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class SetFactEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        [NonSerialized] bool allowReserved;
        public SetFactEffect() { }
        internal SetFactEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public SetFactEffect(string fact, int value, bool allowReserved = false)
        { arguments = new EffectArguments(new object[] { fact, new IntLiteral(value) }); this.allowReserved = allowReserved; }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("set_fact", binder);
        public void Apply(ILogicContext context)
        {
            if (!CanApply(context))
            { Game.Core.GameLog.ErrorOnce("NAR.Effect.InvalidFact.set_fact", "Narrative", "set_fact requires a bound writable fact; effect skipped."); return; }
            context.Facts.SetBySlot(arguments.FactSlots[0], (arguments.Booleans[1] != null ? (arguments.Booleans[1].Evaluate(context) ? 1 : 0) : arguments.Integers[1].Evaluate(context)), FactWriteSource.Effect);
        }
        public bool CanApply(ILogicContext context)
        {
            if (arguments.FactSlots == null || arguments.FactSlots.Length == 0) return false;
            int slot = arguments.FactSlots[0]; var catalog = context.Facts.Catalog;
            return slot >= 0 && slot < catalog.Count && (allowReserved || catalog.IsWritableByContent(slot));
        }
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
