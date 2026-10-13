using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static Game.Editor.Narrative.NarrativeValidationContext;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-40 §5.7: scratch compilation, root validation, fact rules and ordered extensions.</summary>
    public sealed class NarrativeValidator : IContentDatabaseValidator
    {
        public int Order => 100;
        public void Validate(ContentCatalog catalog, ValidationReport report) => Validate(catalog, report, Providers(), Unwired());

        internal void Validate(ContentCatalog catalog, ValidationReport report, IEnumerable<INarrativeRuleProvider> providers,
            IReadOnlyCollection<string> unwired)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (report == null) throw new ArgumentNullException(nameof(report));
            var refs = RefResolverRegistry.CreateDefault(catalog);
            var facts = FactCatalog.Build(catalog, refs, report);
            var compiler = new LogicCompiler(facts, refs, new LogicFunctionRegistry());
            var context = new NarrativeValidationContext(catalog, facts, refs, compiler, unwired);
            var copies = new List<ContentDefinition>();
            try
            {
                foreach (var definition in Owners(catalog))
                {
                    string path = PathOf(definition); bool debug = IsDebug(definition);
                    foreach (var field in Wrappers(definition))
                    {
                        if (Stale(field.Wrapper)) StaleError(report, path, field.Field, "Stored compiled logic is stale; recompile this field.");
                        context.Usage.CheckBoolComparisons(new CompiledGraph(definition, path, field.Field, field.Wrapper, debug), report);
                    }
                    var copy = Object.Instantiate(definition); copies.Add(copy);
                    var owner = (ILogicSourceOwner)copy;
                    try
                    {
                        // A no-op owner must not inherit a valid stamp from its asset and look recompiled.
                        using (var serialized = new SerializedObject(copy))
                        {
                            var iterator = serialized.GetIterator(); bool descend = true;
                            while (iterator.Next(descend))
                            {
                                descend = true;
                                if (iterator.type != nameof(CompiledCondition) && iterator.type != nameof(CompiledIntExpr) && iterator.type != nameof(CompiledEffectList)) continue;
                                iterator.FindPropertyRelative("compilerVersion").intValue = -1; descend = false;
                            }
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                        }
                        owner.CompileLogic(compiler, report.For(path));
                    }
                    catch (Exception exception)
                    {
                        StaleError(report, path, "", "Logic owner could not compile: " + exception.GetType().Name + ": " + exception.Message);
                        continue;
                    }
                    // Bind the copy, and attach owner-level failures to its real asset path.
                    var binding = new ValidationReport(path);
                    try { owner.BindLogic(new LogicBinder(facts, refs, binding)); }
                    catch (Exception exception) { binding.Error("NAR-E017", "", "", "Logic owner could not bind: " + exception.GetType().Name + ": " + exception.Message); }
                    Copy(binding, report, path, "");
                    foreach (var field in Wrappers(copy))
                    {
                        if (Stale(field.Wrapper)) StaleError(report, path, field.Field, "Logic owner did not compile this field.");
                        var local = new ValidationReport(path);
                        try
                        {
                            if (field.Wrapper is ICondition condition) condition.Validate(local);
                            else if (field.Wrapper is IIntExpr expression) expression.Validate(local);
                            else if (Value(field.Wrapper, "effects") is IEffect[] effects)
                                foreach (var effect in effects) effect?.Validate(local);
                        }
                        catch (Exception exception) { local.Error("NAR-E017", "", "", "Compiled root could not validate: " + exception.GetType().Name + ": " + exception.Message); }
                        Copy(local, report, path, field.Field);
                        context.AddGraph(definition, path, field.Field, field.Wrapper, debug);
                    }
                    if (copy is DialogProgramDefinition program) context.Usage.AnalyzeAssignments(program);
                }
                Rules(context, report);
                foreach (var provider in providers.OrderBy(p => p.Order).ThenBy(p => p.GetType().FullName, StringComparer.Ordinal))
                    try { provider.Run(context, report); }
                    catch (Exception exception) { report.Error("DATA-E099", provider.GetType().FullName, "", "Narrative rule provider threw " + exception.GetType().Name + ": " + exception.Message); }
            }
            finally { foreach (var copy in copies) if (copy != null) Object.DestroyImmediate(copy); }
        }

        static void Rules(NarrativeValidationContext context, ValidationReport report)
        {
            var facts = context.Facts;
            var authored = new Dictionary<int, (string Path, string Field)>();
            foreach (var catalog in context.Content.All<FactCatalogDefinition>())
                for (int i = 0; i < catalog.Facts.Count; i++)
                {
                    int slot = facts.SlotOf(catalog.Facts[i].Name);
                    if (slot >= 0 && facts.FamilyAt(slot) == FactFamily.Authored)
                        authored.TryAdd(slot, (PathOf(catalog), "facts[" + i + "]"));
                }
            foreach (var row in authored.OrderBy(r => r.Key))
            {
                int slot = row.Key; var usage = context.Usage.Slots[slot]; var flags = facts.FlagsAt(slot);
                string name = facts.IdAt(slot).Name;
                if (usage.Reads > 0 && usage.Writes == 0 && usage.NeedsNonDefault && (flags & FactFlags.Debug) == 0)
                    report.Error("NAR-V001", usage.ReadAssetPath ?? row.Value.Path, usage.ReadField ?? row.Value.Field,
                        "Fact '" + name + "' needs a non-default value but is never written.");
                if (usage.Writes > 0 && usage.Reads == 0)
                    report.Warning("NAR-V002", row.Value.Path, row.Value.Field, "Fact '" + name + "' is written but never read.");
                if (usage.Reads == 0 && usage.Writes == 0 && (flags & FactFlags.Debug) == 0)
                    report.Error("NAR-V003", row.Value.Path, row.Value.Field, "Fact '" + name + "' is neither read nor written.");
            }
            foreach (var clue in context.Content.All<ClueDefinition>())
            {
                int slot = facts.SlotOf(clue.IdString);
                if (slot >= 0 && context.Usage.Slots[slot].Writes == 0 && !IsDebug(clue))
                    report.Error("NAR-V004", PathOf(clue), "", "Clue '" + clue.IdString + "' is never revealed.");
            }
            int configs = context.Content.All<NarrativeConfigDefinition>().Count;
            if (configs != 1)
            {
                report.Error("NAR-V005", "ContentDatabase", "narrativeConfig", "Expected exactly one NarrativeConfigDefinition; found " + configs + ".");
                report.Error("NAR-V010", "ContentDatabase", "narrativeConfig", "Expected exactly one NarrativeConfigDefinition; found " + configs + ".");
            }
            foreach (var use in context.Usage.WorldUses.Distinct())
                if (context.UnwiredMembers.Contains(use.Member))
                {
                    report.Warning("NAR-V006", use.AssetPath, use.Field, "Content uses unwired LogicWorldAdapter member '" + use.Member + "'.");
                    report.Warning("NAR-W020", use.AssetPath, use.Field, "LogicWorldAdapter member '" + use.Member + "' is not wired.");
                }
        }
        static bool Stale(object wrapper) => wrapper switch {
            CompiledCondition condition => condition.IsStale, CompiledIntExpr expression => expression.IsStale,
            CompiledEffectList effects => effects.IsStale, _ => false
        };
        static void StaleError(ValidationReport report, string path, string field, string message)
        {
            report.Error("NAR-E017", path, field, message);
            report.Error("NAR-V007", path, field, message);
        }
        static void Copy(ValidationReport local, ValidationReport report, string path, string field)
        {
            foreach (var entry in local.Entries)
                report.Add(entry.Severity, entry.Code, path, field.Length == 0 ? entry.Field :
                    entry.Field.Length == 0 ? field : field + "." + entry.Field, entry.Message);
        }
        static IEnumerable<INarrativeRuleProvider> Providers() => TypeCache.GetTypesDerivedFrom<INarrativeRuleProvider>()
            .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => (INarrativeRuleProvider)Activator.CreateInstance(type));
        static IEnumerable<ContentDefinition> Owners(ContentCatalog catalog)
        {
            // ContentCatalog registers concrete families, not the ContentDefinition base family.
            var owners = new HashSet<ContentDefinition>();
            var all = typeof(ContentCatalog).GetMethod(nameof(ContentCatalog.All));
            foreach (var type in TypeCache.GetTypesDerivedFrom<ContentDefinition>().Where(type => typeof(ILogicSourceOwner).IsAssignableFrom(type)))
                foreach (ContentDefinition owner in (System.Collections.IEnumerable)all.MakeGenericMethod(type).Invoke(catalog, null)) owners.Add(owner);
            return owners.OrderBy(owner => owner.IdString, StringComparer.Ordinal);
        }
        static IReadOnlyCollection<string> Unwired()
        {
            // The adapter belongs to NAR-011. Read its actual declared set when present; no duplicated wiring table.
            var type = TypeCache.GetTypesDerivedFrom<ILogicWorld>().FirstOrDefault(t => t.FullName == "Game.App.LogicWorldAdapter");
            var value = type?.GetField("UnwiredMembers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                ?? type?.GetProperty("UnwiredMembers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            if (value is IEnumerable<string> members) return members.ToArray();
            // No adapter/wiring manifest exists yet: each used world member is currently unavailable.
            return typeof(ILogicWorld).GetMethods().Select(method => method.Name.StartsWith("get_", StringComparison.Ordinal) ? method.Name.Substring(4) : method.Name).ToArray();
        }
    }
}
