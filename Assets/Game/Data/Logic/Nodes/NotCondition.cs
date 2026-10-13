using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class NotCondition : ICondition
    {
        [SerializeReference] private ICondition operand;
        public NotCondition() { }
        internal NotCondition(ICondition operand) { this.operand = operand; }
        public bool Evaluate(ILogicContext context) => operand != null && !operand.Evaluate(context);
        public void Validate(ValidationReport report)
        {
            if (operand == null) report.Error("NAR-E009", "", "", "Compiled node is missing an operand.");
            else operand.Validate(report);
        }
        public void Bind(LogicBinder binder) => operand?.Bind(binder);
        public void CollectDependencies(List<LogicDependency> into) => operand?.CollectDependencies(into);
    }
}
