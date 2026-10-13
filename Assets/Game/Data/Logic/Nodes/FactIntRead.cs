using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class FactIntRead : IIntExpr
    {
        [SerializeField] private string name = "";
        [NonSerialized] private int slot = -1;
        public FactIntRead() { }
        internal FactIntRead(string name) { this.name = name; }
        // TODO(NAR-004): evaluate the bound slot, failing closed when unbound.
        public int Evaluate(ILogicContext context) => throw new NotImplementedException("NAR-004");
        public void Validate(ValidationReport report) { }
        public void Bind(LogicBinder binder) { slot = binder.BindFact(name); }
        public void CollectDependencies(List<LogicDependency> into)
        {
            if (slot >= 0)
            {
                var dependency = new LogicDependency(LogicDependencyKind.Fact, slot);
                if (!into.Contains(dependency)) into.Add(dependency);
            }
        }
    }
}
