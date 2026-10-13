using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class CompareBoolCondition : ICondition
    {
        [SerializeField] private CompareOp op;
        [SerializeReference] private ICondition left, right;
        public CompareOp Op => op;
        public CompareBoolCondition() { }
        internal CompareBoolCondition(CompareOp op, ICondition left, ICondition right)
        { this.op = op; this.left = left; this.right = right; }
        // TODO(NAR-004): evaluate the typed comparison.
        public bool Evaluate(ILogicContext context) => throw new NotImplementedException("NAR-004");
        public void Validate(ValidationReport report) { left?.Validate(report); right?.Validate(report); }
        public void Bind(LogicBinder binder) { left?.Bind(binder); right?.Bind(binder); }
        public void CollectDependencies(List<LogicDependency> into) { left?.CollectDependencies(into); right?.CollectDependencies(into); }
    }
}
