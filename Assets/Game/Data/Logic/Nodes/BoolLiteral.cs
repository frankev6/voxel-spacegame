using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class BoolLiteral : ICondition
    {
        [SerializeField] private bool value;
        public BoolLiteral() { }
        internal BoolLiteral(bool value) { this.value = value; }
        public bool Evaluate(ILogicContext context) => value;
        public void Validate(ValidationReport report) { }
        public void Bind(LogicBinder binder) { }
        public void CollectDependencies(List<LogicDependency> into) { }
    }
}
