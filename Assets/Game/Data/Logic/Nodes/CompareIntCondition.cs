using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class CompareIntCondition : ICondition
    {
        [SerializeField] private CompareOp op;
        [SerializeReference] private IIntExpr left, right;
        public CompareOp Op => op;
        public CompareIntCondition() { }
        internal CompareIntCondition(CompareOp op, IIntExpr left, IIntExpr right)
        { this.op = op; this.left = left; this.right = right; }
        public bool Evaluate(ILogicContext context)
        {
            if (left == null || right == null) return false;
            int a = left.Evaluate(context), b = right.Evaluate(context);
            switch (op)
            {
                case CompareOp.Equal: return a == b;
                case CompareOp.NotEqual: return a != b;
                case CompareOp.Less: return a < b;
                case CompareOp.LessEqual: return a <= b;
                case CompareOp.Greater: return a > b;
                case CompareOp.GreaterEqual: return a >= b;
                default: return false;
            }
        }
        public void Validate(ValidationReport report)
        {
            if (left == null || right == null) report.Error("NAR-E009", "", "", "Compiled node is missing an operand.");
            if ((byte)op > (byte)CompareOp.GreaterEqual) report.Error("NAR-E010", "", "", "Unknown integer comparison operator.");
            left?.Validate(report); right?.Validate(report);
        }
        public void Bind(LogicBinder binder) { left?.Bind(binder); right?.Bind(binder); }
        public void CollectDependencies(List<LogicDependency> into) { left?.CollectDependencies(into); right?.CollectDependencies(into); }
    }
}
