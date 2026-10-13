using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    public sealed class IntFunctionExpr : IIntExpr
    {
        [SerializeField] private string function = "";
        [SerializeReference] private IIntExpr[] intArgs = Array.Empty<IIntExpr>();
        [SerializeReference] private ICondition[] boolArgs = Array.Empty<ICondition>();
        [SerializeField] private string[] textArgs = Array.Empty<string>();
        [NonSerialized] private LogicFunctionInfo info;
        [NonSerialized] private int[] indices;
        [NonSerialized] private int[] factSlots;
        public IntFunctionExpr() { }
        internal IntFunctionExpr(LogicFunctionInfo info, ReadOnlySpan<object> arguments)
        {
            function = info.Name;
            intArgs = new IIntExpr[arguments.Length]; boolArgs = new ICondition[arguments.Length];
            textArgs = new string[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                intArgs[i] = arguments[i] as IIntExpr;
                boolArgs[i] = arguments[i] as ICondition;
                textArgs[i] = arguments[i] as string;
            }
        }
        // TODO(NAR-004): evaluate the bound vocabulary function without allocations.
        public int Evaluate(ILogicContext context) => throw new NotImplementedException("NAR-004");
        public void Validate(ValidationReport report)
        {
            if (!new LogicFunctionRegistry().TryGet(function, out var metadata) || metadata.IsEffect)
                report.Error("NAR-E003", "", "", "Unknown function '" + function + "'.");
            foreach (var arg in intArgs) arg?.Validate(report);
            foreach (var arg in boolArgs) arg?.Validate(report);
        }
        public void Bind(LogicBinder binder)
        {
            if (!new LogicFunctionRegistry().TryGet(function, out info))
                throw new InvalidOperationException("Unknown compiled function: " + function);
            indices = new int[info.Params.Length]; factSlots = new int[info.Params.Length];
            for (int i = 0; i < info.Params.Length; i++)
            {
                factSlots[i] = -1;
                intArgs[i]?.Bind(binder); boolArgs[i]?.Bind(binder);
                var parameter = info.Params[i];
                if (parameter.ArgKind == LogicArgKind.Fact) factSlots[i] = binder.BindFact(textArgs[i]);
                if (parameter.ArgKind != LogicArgKind.Ref) continue;
                if (parameter.RefKind == RefKind.Faction && textArgs[i] == "owner") indices[i] = 0;
                else indices[i] = binder.BindRef(parameter.RefKind, textArgs[i]);
                if (parameter.RefKind == RefKind.Clue || parameter.RefKind == RefKind.Beat || parameter.RefKind == RefKind.Mystery)
                    factSlots[i] = binder.BindFact(textArgs[i]);
            }
        }
        public void CollectDependencies(List<LogicDependency> into)
        {
            foreach (var arg in intArgs) arg?.CollectDependencies(into);
            foreach (var arg in boolArgs) arg?.CollectDependencies(into);
            if (info == null) return;
            for (int i = 0; i < info.Params.Length; i++)
            {
                if (factSlots[i] >= 0) Add(LogicDependencyKind.Fact, factSlots[i]);
                if (info.Params[i].ArgKind != LogicArgKind.Ref) continue;
                switch (info.Params[i].RefKind)
                {
                    case RefKind.Item: Add(LogicDependencyKind.Item, indices[i]); break;
                    case RefKind.Quest: Add(LogicDependencyKind.Quest, indices[i]); break;
                    case RefKind.Faction: Add(LogicDependencyKind.Relation, indices[i]); break;
                }
            }
            if (function == "credits") Add(LogicDependencyKind.Credits, 0);
            if (function == "game_hours") Add(LogicDependencyKind.Time, 0);
            if (function == "in_star_system" || function == "star_system_owned") Add(LogicDependencyKind.Location, 0);
            void Add(LogicDependencyKind kind, int key)
            {
                var dependency = new LogicDependency(kind, key);
                if (!into.Contains(dependency)) into.Add(dependency);
            }
        }
    }
}
