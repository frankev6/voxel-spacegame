using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class NegateExpr : IIntExpr
    {
        [SerializeReference] private IIntExpr operand;
        public NegateExpr() { }
        internal NegateExpr(IIntExpr operand) { this.operand = operand; }
        public int Evaluate(ILogicContext context) => operand != null ? LogicFunctionInfo.Saturate(-(long)operand.Evaluate(context)) : 0;
        public void Validate(ValidationReport report)
        {
            if (operand == null) report.Error("NAR-E009", "", "", "Compiled node is missing an operand.");
            else operand.Validate(report);
        }
        public void Bind(LogicBinder binder) => operand?.Bind(binder);
        public void CollectDependencies(List<LogicDependency> into) => operand?.CollectDependencies(into);
    }
}
