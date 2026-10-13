using System;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;

namespace Game.Data.Logic
{
    /// <summary>Resolves authored names to runtime slots and indices, retaining diagnostics for failed bindings.</summary>
    public sealed class LogicBinder
    {
        readonly FactCatalog facts;
        readonly RefResolverRegistry refs;
        readonly ValidationReport report;
        public int FailedBindings { get; private set; }

        public LogicBinder(FactCatalog facts, RefResolverRegistry refs, ValidationReport report)
        {
            this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
            this.refs = refs ?? throw new ArgumentNullException(nameof(refs));
            this.report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public int BindFact(string name)
        {
            int slot = facts.SlotOf(name);
            if (slot >= 0) return slot;
            string suggestion = null; int best = 3;
            for (int i = 0; i < facts.Count; i++)
            {
                string candidate = facts.IdAt(i).Name;
                int distance = LogicFunctionRegistry.Distance(name, candidate);
                if (distance < best || distance == best && distance <= 2 && string.CompareOrdinal(candidate, suggestion) < 0)
                { suggestion = candidate; best = distance; }
            }
            FailedBindings++;
            report.Error("NAR-E006", "", "", "Unknown fact '" + name + "'" + Suggestion(suggestion) + ".");
            return -1;
        }

        public int BindRef(RefKind kind, string id)
        {
            if (!refs.TryGet(kind, out var resolver))
            {
                report.Warning("NAR-W001", "", "", "No resolver is registered for " + kind + ".");
                return 0;
            }
            if (resolver.TryResolve(id, out int index)) return index;
            string suggestion = null; int best = 3;
            foreach (string candidate in resolver.AllIds)
            {
                int distance = LogicFunctionRegistry.Distance(id, candidate);
                if (distance < best || distance == best && distance <= 2 && string.CompareOrdinal(candidate, suggestion) < 0)
                { suggestion = candidate; best = distance; }
            }
            FailedBindings++;
            report.Error("NAR-E007", "", "", "Unknown " + kind + " '" + id + "'" + Suggestion(suggestion) + ".");
            return 0;
        }

        static string Suggestion(string value) => value == null ? "" : "; did you mean '" + value + "'?";
    }
}
