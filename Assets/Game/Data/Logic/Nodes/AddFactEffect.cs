using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class AddFactEffect : IEffect
    {
        [SerializeField] private EffectArguments arguments = new EffectArguments();
        [NonSerialized] bool allowReserved;
        public AddFactEffect() { }
        internal AddFactEffect(ReadOnlySpan<object> arguments) { this.arguments = new EffectArguments(arguments); }
        public AddFactEffect(string fact, int value, bool allowReserved = false)
        { arguments = new EffectArguments(new object[] { fact, new IntLiteral(value) }); this.allowReserved = allowReserved; }
        public EffectFlow Flow => EffectFlow.Immediate;
        public void Validate(ValidationReport report) => arguments.Validate(report);
        public void Bind(LogicBinder binder) => arguments.Bind("add_fact", binder);
        public void Apply(ILogicContext context)
        {
            if (!CanApply(context))
            { Game.Core.GameLog.ErrorOnce("NAR.Effect.InvalidFact.add_fact", "Narrative", "add_fact requires a bound writable fact; effect skipped."); return; }
            context.Facts.AddBySlot(arguments.FactSlots[0], arguments.Integers[1].Evaluate(context), FactWriteSource.Effect);
        }
        public bool CanApply(ILogicContext context)
        {
            if (arguments.FactSlots == null || arguments.FactSlots.Length == 0) return false;
            int slot = arguments.FactSlots[0]; var catalog = context.Facts.Catalog;
            return slot >= 0 && slot < catalog.Count && (allowReserved || catalog.IsWritableByContent(slot)) && catalog.KindAt(slot) == FactKind.Int;
        }
        public void AddRequirements(EffectRequirements requirements) { }
    }
}
