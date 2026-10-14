#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Economy;
using Game.Narrative;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>DEV-017 probes over the canonical economy and fact owners.</summary>
    public static class StateScenarioProbes
    {
        sealed class Probe : IScenarioProbe
        {
            readonly Dictionary<string, ICondition> conditions = new Dictionary<string, ICondition>(StringComparer.Ordinal);
            FactCatalog factCatalog;
            ContentCatalog contentCatalog;
            public string Name { get; }
            public int ArgCount => Name == "credits" ? 0 : 1;
            internal Probe(string name) { Name = name; }
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args)
            {
                if (Name == "credits") return Number(DevState.Economy(ctx).Wallet.Credits);
                string text = RequireString(args[0]);
                if (Name == "inv") return Number(DevState.Economy(ctx).Inventory.CountOf(new ContentId(text)));
                var store = DevState.Facts(ctx);
                if (Name == "fact") return Number(store.GetBySlot(DevState.FactSlot(store, text)));
                var content = DevState.Content(ctx);
                // Rebind on a real catalog replacement. Repeated scenario polling only evaluates cached nodes.
                if (!ReferenceEquals(factCatalog, store.Catalog) || !ReferenceEquals(contentCatalog, content))
                { conditions.Clear(); factCatalog = store.Catalog; contentCatalog = content; }
                if (!conditions.TryGetValue(text, out var condition))
                { condition = DevState.Compile(text, factCatalog, contentCatalog); conditions.Add(text, condition); }
                return Boolean(DevState.Evaluate(condition, ctx, store));
            }
        }
        internal static void Register(ScenarioProbeRegistry registry)
        { foreach (string name in new[] { "fact", "cond", "inv", "credits" }) registry.Register(new Probe(name)); }
    }

    /// <summary>Shared dev-only owner lookup and fail-closed compiler path; no production contract changes.</summary>
    internal static class DevState
    {
        internal static EconomyService Economy(DevContext ctx) => ctx.TryGetService<EconomyService>(out var economy)
            ? economy : throw new InvalidOperationException("economy unavailable");
        internal static FactStore Facts(DevContext ctx) => ctx.TryGetService<FactStore>(out var facts)
            ? facts : throw new InvalidOperationException("facts unavailable");
        internal static ContentCatalog Content(DevContext ctx) => ctx.TryGetService<ContentService>(out var content)
            ? content.Catalog : throw new InvalidOperationException("content unavailable");
        internal static void CompleteWrites(DevContext ctx)
        {
            if (ctx.World == null || !ctx.World.IsCreated) throw new InvalidOperationException("world unavailable");
            ctx.EntityManager.CompleteAllTrackedJobs();
        }
        internal static int FactSlot(FactStore store, string name)
        {
            int slot = store.Catalog.SlotOf(name);
            if (slot < 0) throw new InvalidOperationException("NAR-E006: Unknown fact '" + name + "'.");
            return slot;
        }
        internal static ICondition Compile(string source, FactCatalog facts, ContentCatalog content)
        {
            var report = new ValidationReport("dev condition"); var refs = RefResolverRegistry.CreateDefault(content);
            var compiler = new LogicCompiler(facts, refs, new LogicFunctionRegistry());
            if (!compiler.TryCompileExpression(source, new LogicCompileOptions(LogicUsage.BeatCondition, "dev", "eval"), report, out var condition))
                throw new InvalidOperationException(report.ToText());
            condition?.Bind(new LogicBinder(facts, refs, report));
            if (report.HasErrors) throw new InvalidOperationException(report.ToText());
            return condition;
        }
        internal static bool Evaluate(ICondition condition, DevContext ctx, FactStore store)
        {
            if (!ctx.TryGetService<ILogicContext>(out var logic)) throw new InvalidOperationException("logic context unavailable");
            if (!ReferenceEquals(logic.Facts, store)) throw new InvalidOperationException("logic context and fact store differ");
            logic.PushScope(new LogicScope(LogicUsage.BeatCondition, 0, 0, 0, false, default));
            try { return condition?.Evaluate(logic) ?? true; }
            finally { logic.PopScope(); }
        }
    }
}
#endif
