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
        public bool Evaluate(ILogicContext context)
        {
            if (left == null || right == null) return false;
            bool a = left.Evaluate(context), b = right.Evaluate(context);
            return op == CompareOp.Equal ? a == b : op == CompareOp.NotEqual && a != b;
        }
        public void Validate(ValidationReport report)
        {
            if (left == null || right == null) report.Error("NAR-E009", "", "", "Compiled node is missing an operand.");
            if (op != CompareOp.Equal && op != CompareOp.NotEqual) report.Error("NAR-E010", "", "", "Bool comparison supports only == and !=.");
            left?.Validate(report); right?.Validate(report);
        }
        public void Bind(LogicBinder binder) { left?.Bind(binder); right?.Bind(binder); }
        public void CollectDependencies(List<LogicDependency> into) { left?.CollectDependencies(into); right?.CollectDependencies(into); }
    }
}
