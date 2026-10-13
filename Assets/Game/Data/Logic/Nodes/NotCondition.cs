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
        // TODO(NAR-004): evaluate logical negation or saturating integer negation.
        public bool Evaluate(ILogicContext context) => throw new NotImplementedException("NAR-004");
        public void Validate(ValidationReport report) => operand?.Validate(report);
        public void Bind(LogicBinder binder) => operand?.Bind(binder);
        public void CollectDependencies(List<LogicDependency> into) => operand?.CollectDependencies(into);
    }
}
