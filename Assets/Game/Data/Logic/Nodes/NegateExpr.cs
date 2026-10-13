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
        // TODO(NAR-004): evaluate logical negation or saturating integer negation.
        public int Evaluate(ILogicContext context) => throw new NotImplementedException("NAR-004");
        public void Validate(ValidationReport report) => operand?.Validate(report);
        public void Bind(LogicBinder binder) => operand?.Bind(binder);
        public void CollectDependencies(List<LogicDependency> into) => operand?.CollectDependencies(into);
    }
}
