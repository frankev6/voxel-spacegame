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
        [NonSerialized] private FunctionEvaluation evaluation;
        [NonSerialized] private int[] ownerRelations = Array.Empty<int>();
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
        public int Evaluate(ILogicContext context) => evaluation != null ? evaluation.Evaluate(context).Int : 0;
        public void Validate(ValidationReport report)
        {
            if (!new LogicFunctionRegistry().TryGet(function, out var metadata) || metadata.IsEffect)
            { report.Error("NAR-E003", "", "", "Unknown function '" + function + "'."); return; }
            if (metadata.ReturnType != LogicType.Int)
            { report.Error("NAR-E005", "", "", "Function return type does not match the compiled node."); return; }
            if (!FunctionEvaluation.ValidateShape(metadata, intArgs, boolArgs, textArgs, report)) return;
            foreach (var arg in intArgs) arg?.Validate(report);
            foreach (var arg in boolArgs) arg?.Validate(report);
        }
        public void Bind(LogicBinder binder)
        {
            evaluation = null; ownerRelations = Array.Empty<int>();
            if (!new LogicFunctionRegistry().TryGet(function, out info))
                throw new InvalidOperationException("Unknown compiled function: " + function);
            if (info.IsEffect || info.ReturnType != LogicType.Int || intArgs == null || boolArgs == null || textArgs == null ||
                intArgs.Length != info.Params.Length || boolArgs.Length != info.Params.Length || textArgs.Length != info.Params.Length)
                throw new InvalidOperationException("Invalid compiled function shape: " + function);
            indices = new int[info.Params.Length]; factSlots = new int[info.Params.Length];
            for (int i = 0; i < info.Params.Length; i++)
            {
                factSlots[i] = -1;
                intArgs[i]?.Bind(binder); boolArgs[i]?.Bind(binder);
                var parameter = info.Params[i];
                if (parameter.ArgKind == LogicArgKind.Fact) factSlots[i] = binder.BindFact(textArgs[i]);
                if (parameter.ArgKind != LogicArgKind.Ref) continue;
                if (parameter.RefKind == RefKind.Faction && textArgs[i] == "owner")
                { indices[i] = -1; ownerRelations = binder.BindRelationDependencies(); }
                else indices[i] = binder.BindRef(parameter.RefKind, textArgs[i]);
                if (parameter.RefKind == RefKind.Clue || parameter.RefKind == RefKind.Beat || parameter.RefKind == RefKind.Mystery)
                    factSlots[i] = binder.BindFact(textArgs[i]);
            }
            evaluation = new FunctionEvaluation(info, intArgs, boolArgs, textArgs, indices, factSlots);
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
                    case RefKind.Faction:
                        if (indices[i] == -1)
                        {
                            Add(LogicDependencyKind.Location, 0);
                            foreach (int faction in ownerRelations) Add(LogicDependencyKind.Relation, faction);
                        }
                        else Add(LogicDependencyKind.Relation, indices[i]);
                        break;
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
