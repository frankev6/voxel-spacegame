using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class FactBoolRead : ICondition
    {
        [SerializeField] private string name = "";
        [NonSerialized] private int slot = -1;
        public FactBoolRead() { }
        internal FactBoolRead(string name) { this.name = name; }
        public bool Evaluate(ILogicContext context) => slot >= 0 && context.Facts.GetBySlot(slot) != 0;
        public void Validate(ValidationReport report)
        { if (string.IsNullOrEmpty(name)) report.Error("NAR-E006", "", "", "Compiled fact read has no name."); }
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
