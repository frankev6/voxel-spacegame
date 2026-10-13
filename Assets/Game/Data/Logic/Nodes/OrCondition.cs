using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class OrCondition : ICondition
    {
        [SerializeReference] private ICondition left, right;
        public OrCondition() { }
        internal OrCondition(ICondition left, ICondition right) { this.left = left; this.right = right; }
        public bool Evaluate(ILogicContext context) => left != null && right != null && (left.Evaluate(context) || right.Evaluate(context));
        public void Validate(ValidationReport report)
        {
            if (left == null || right == null) report.Error("NAR-E009", "", "", "Compiled node is missing an operand.");
            left?.Validate(report); right?.Validate(report);
        }
        public void Bind(LogicBinder binder) { left?.Bind(binder); right?.Bind(binder); }
        public void CollectDependencies(List<LogicDependency> into) { left?.CollectDependencies(into); right?.CollectDependencies(into); }
    }
}
