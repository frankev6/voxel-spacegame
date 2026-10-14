using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.Narrative.Dialog
{
    public static class DialogCommandTableGenerator
    {
        public const string Root = "Assets/Game/Content/Narrative/Dialog";
        internal sealed class Plan
        {
            internal ulong Hash;
            internal string Facts, Ysls;
            internal readonly SortedSet<string> Commands = new(StringComparer.Ordinal);
        }
        internal static NarrativeValidationContext Context(ContentCatalog content, ValidationReport report)
        {
            var refs = RefResolverRegistry.CreateDefault(content);
            var facts = FactCatalog.Build(content, refs, report);
            return new NarrativeValidationContext(content, facts, refs, new LogicCompiler(facts, refs, new LogicFunctionRegistry()), Array.Empty<string>());
        }
        internal static SortedDictionary<string, string> ReadSources(string root)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (Directory.Exists(root))
                foreach (string path in Directory.GetFiles(root, "*.yarn", SearchOption.AllDirectories))
                {
                    string normalized = path.Replace('\\', '/');
                    if (normalized == root + "/_Generated/facts.yarn") continue;
                    result.Add(normalized, File.ReadAllText(path));
                }
            return result;
        }
        internal static List<DialogScriptAst> Parse(IReadOnlyDictionary<string, string> sources)
            => sources.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => DialogScriptParser.Parse(pair.Key, pair.Value)).ToList();
        internal static Plan Prepare(NarrativeValidationContext ctx, IReadOnlyDictionary<string, string> sources, string root)
        {
            var plan = new Plan { Facts = FactsYarnGenerator.Build(ctx.Facts), Ysls = YslsGenerator.Build(new LogicFunctionRegistry()) };
            var hashed = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in sources)
                if (pair.Key != root + "/_Generated/facts.yarn") hashed.Add(pair.Key, pair.Value);
            // Include the expected declaration file, not yesterday's output: first generation is already stable.
            hashed[root + "/_Generated/facts.yarn"] = plan.Facts;
            foreach (var pair in hashed)
            {
                string path = pair.Key.StartsWith(root + "/", StringComparison.Ordinal) ? pair.Key.Substring(root.Length + 1) : pair.Key;
                plan.Hash = StableHash.Combine(plan.Hash, StableHash.Combine(StableHash.Hash64(path), StableHash.Hash64(pair.Value)));
            }
            foreach (var script in Parse(sources))
                foreach (var node in script.Nodes)
                    foreach (var statement in DialogSyntaxTraversal.Walk(node.Body))
                        if (statement is CommandStmt command) plan.Commands.Add(command.Text);
            return plan;
        }
        internal static bool CommandsCurrent(DialogCommandTableDefinition table, Plan plan)
        {
            if (table == null || table.Entries == null || table.Entries.Count != plan.Commands.Count) return false;
            int i = 0;
            foreach (string text in plan.Commands)
            {
                var entry = table.Entries[i++];
                if (entry.Text != text || entry.Effect == null || entry.Effect.Source.Count != 1 || entry.Effect.Source[0] != text ||
                    entry.Effect.IsStale || entry.Effect.HasErrors || entry.Effect.IsEmpty) return false;
            }
            return true;
        }
        internal static bool BytesCurrent(string path, string expected)
            => File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(new UTF8Encoding(false).GetBytes(expected));
        internal static void WriteText(string path, string text)
        {
            if (BytesCurrent(path, text)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        public static ValidationReport Generate(ContentCatalog content, string root = Root)
        {
            var report = new ValidationReport("Dialog generation");
            var ctx = Context(content, report);
            return Generate(ctx, ReadSources(root), root, report);
        }
        internal static ValidationReport Generate(NarrativeValidationContext ctx, IReadOnlyDictionary<string, string> sources, string root, ValidationReport report)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { report.Error("DLG-E013", root, "", "Dialog generation requires Edit mode."); return report; }
            var scripts = Parse(sources);
            new DialogValidatorRules().Validate(ctx, scripts, report);
            if (report.HasErrors) return report;
            var plan = Prepare(ctx, sources, root);
            string directory = root + "/_Generated", path = directory + "/dialog_command_table.asset";
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is DialogCommandTableDefinition) || existing == null && File.Exists(path))
            { report.Error("DLG-E013", path, "", "Generated command-table path has another asset type."); return report; }
            if (Directory.Exists(path) || File.Exists(directory) || Directory.Exists(directory + "/facts.yarn") || Directory.Exists(directory + "/logic.ysls.json"))
            { report.Error("DLG-E013", directory, "", "A generated output path has the wrong filesystem type."); return report; }
            var table = existing as DialogCommandTableDefinition;
            var other = ctx.Content.All<DialogCommandTableDefinition>().FirstOrDefault(value => value.IdString == "dialog_command_table" && value != table);
            if (other != null)
            { report.Error("DLG-E013", path, "", "dialog_command_table already exists outside the generated destination."); return report; }
            if (!CommandsCurrent(table, plan))
            {
                var copy = ScriptableObject.CreateInstance<DialogCommandTableDefinition>(); copy.name = "dialog_command_table";
                try
                {
                    using (var serialized = new SerializedObject(copy))
                    {
                        serialized.FindProperty("id").stringValue = "dialog_command_table";
                        serialized.FindProperty("sourcesHash").ulongValue = plan.Hash;
                        var rows = serialized.FindProperty("entries"); rows.arraySize = plan.Commands.Count; int index = 0;
                        foreach (string text in plan.Commands)
                        {
                            var row = rows.GetArrayElementAtIndex(index++); row.FindPropertyRelative("Text").stringValue = text;
                            var source = row.FindPropertyRelative("Effect.source"); source.arraySize = 1; source.GetArrayElementAtIndex(0).stringValue = text;
                        }
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    copy.CompileLogic(ctx.Compiler, report.For(path));
                    if (report.HasErrors) return report;
                    if (table == null)
                    {
                        Directory.CreateDirectory(directory); AssetDatabase.Refresh(); AssetDatabase.CreateAsset(copy, path); table = copy; copy = null;
                    }
                    else { EditorUtility.CopySerialized(copy, table); EditorUtility.SetDirty(table); AssetDatabase.SaveAssetIfDirty(table); }
                }
                finally { if (copy != null) Object.DestroyImmediate(copy); }
            }
            else if (table.SourcesHash != plan.Hash)
            {
                using var serialized = new SerializedObject(table);
                serialized.FindProperty("sourcesHash").ulongValue = plan.Hash; serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(table);
            }
            WriteText(directory + "/facts.yarn", plan.Facts); WriteText(directory + "/logic.ysls.json", plan.Ysls);
            AssetDatabase.Refresh();
            return report;
        }
    }
}
