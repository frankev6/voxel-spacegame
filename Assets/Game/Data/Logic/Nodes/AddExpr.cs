using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class AddExpr : IIntExpr
    {
        [SerializeReference] private IIntExpr left, right;
        public AddExpr() { }
        internal AddExpr(IIntExpr left, IIntExpr right) { this.left = left; this.right = right; }
        // TODO(NAR-004): evaluate with short-circuit logic or saturating arithmetic.
        public int Evaluate(ILogicContext context) => throw new NotImplementedException("NAR-004");
        public void Validate(ValidationReport report) { left?.Validate(report); right?.Validate(report); }
        public void Bind(LogicBinder binder) { left?.Bind(binder); right?.Bind(binder); }
        public void CollectDependencies(List<LogicDependency> into) { left?.CollectDependencies(into); right?.CollectDependencies(into); }
    }
}
