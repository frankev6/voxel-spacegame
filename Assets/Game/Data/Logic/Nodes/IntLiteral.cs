using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class IntLiteral : IIntExpr
    {
        [SerializeField] private int value;
        public IntLiteral() { }
        internal IntLiteral(int value) { this.value = value; }
        public int Evaluate(ILogicContext context) => value;
        public void Validate(ValidationReport report) { }
        public void Bind(LogicBinder binder) { }
        public void CollectDependencies(List<LogicDependency> into) { }
    }
}
