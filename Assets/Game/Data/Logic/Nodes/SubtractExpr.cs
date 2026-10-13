using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class SubtractExpr : IIntExpr
    {
        [SerializeReference] private IIntExpr left, right;
        public SubtractExpr() { }
        internal SubtractExpr(IIntExpr left, IIntExpr right) { this.left = left; this.right = right; }
        public int Evaluate(ILogicContext context) => left != null && right != null ? LogicFunctionInfo.Saturate((long)left.Evaluate(context) - right.Evaluate(context)) : 0;
        public void Validate(ValidationReport report)
        {
            if (left == null || right == null) report.Error("NAR-E009", "", "", "Compiled node is missing an operand.");
            left?.Validate(report); right?.Validate(report);
        }
        public void Bind(LogicBinder binder) { left?.Bind(binder); right?.Bind(binder); }
        public void CollectDependencies(List<LogicDependency> into) { left?.CollectDependencies(into); right?.CollectDependencies(into); }
    }
}
