using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Logic.Nodes
{
    [Serializable]
    internal sealed class EffectArguments
    {
        [SerializeReference] internal IIntExpr[] Integers = Array.Empty<IIntExpr>();
        [SerializeReference] internal ICondition[] Booleans = Array.Empty<ICondition>();
        [SerializeField] internal string[] Text = Array.Empty<string>();
        [NonSerialized] internal int[] Indices;
        [NonSerialized] internal int[] FactSlots;
        [NonSerialized] bool referencesValid;
        internal EffectArguments() { }
        internal EffectArguments(ReadOnlySpan<object> arguments)
        {
            Integers = new IIntExpr[arguments.Length]; Booleans = new ICondition[arguments.Length];
            Text = new string[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            { Integers[i] = arguments[i] as IIntExpr; Booleans[i] = arguments[i] as ICondition; Text[i] = arguments[i] as string; }
        }
        internal void Validate(ValidationReport report)
        {
            foreach (var value in Integers) value?.Validate(report);
            foreach (var value in Booleans) value?.Validate(report);
        }
        internal void Bind(string name, LogicBinder binder)
        {
            if (!new LogicFunctionRegistry().TryGet(name, out var info)) throw new InvalidOperationException(name);
            Indices = new int[info.Params.Length]; FactSlots = new int[info.Params.Length];
            referencesValid = true;
            for (int i = 0; i < info.Params.Length; i++)
            {
                FactSlots[i] = -1; Integers[i]?.Bind(binder); Booleans[i]?.Bind(binder);
                if (info.Params[i].ArgKind == LogicArgKind.Fact) FactSlots[i] = binder.BindFact(Text[i]);
                if (info.Params[i].ArgKind == LogicArgKind.Ref)
                {
                    bool owner = info.Params[i].RefKind == RefKind.Faction && Text[i] == "owner";
                    Indices[i] = owner ? 0 : binder.BindRef(info.Params[i].RefKind, Text[i]);
                    if (!owner && Indices[i] == 0) referencesValid = false;
                }
            }
        }
        internal bool CanRoute(string operation)
        {
            if (Indices != null && referencesValid) return true;
            Game.Core.GameLog.ErrorOnce("NAR.Effect.Unbound." + operation, "Narrative",
                operation + " has an unresolved reference or has not been bound; effect skipped.");
            return false;
        }
    }
}
