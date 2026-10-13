using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Logic.Nodes;
using Game.Data.Validation;
using static Game.Editor.Narrative.NarrativeValidationContext;

namespace Game.Editor.Narrative
{
    /// <summary>Read-only QST command/reference traversal; never executes conditions or effects.</summary>
    internal static class QuestRuleTraversal
    {
        internal static IEnumerable<(string Name, string[] Args, string Path, string Field)> Commands(NarrativeValidationContext context, ValidationReport report)
        {
            foreach (var graph in context.Graphs)
                foreach (var command in Commands(graph.Root, graph.AssetPath, graph.Field)) yield return command;
            foreach (var table in context.Content.All<DialogCommandTableDefinition>())
            {
                if (context.Graphs.Any(graph => graph.SourceOwner == table && graph.Wrapper is CompiledEffectList) || table.Entries == null) continue;
                // DLG's table is a declared source surface. Validate its command text with the real compiler
                // when that owner has not supplied graphs; its separate owner failures remain in the report.
                for (int i = 0; i < table.Entries.Count; i++)
                {
                    string path = PathOf(table), field = "entries[" + i + "].Text";
                    if (context.Compiler.TryCompileCommand(table.Entries[i].Text, new LogicCompileOptions(LogicUsage.DialogCommand, path, field), report, out var root))
                        foreach (var command in Commands(root, path, field)) yield return command;
                }
            }
        }
        static IEnumerable<(string Name, string[] Args, string Path, string Field)> Commands(object root, string path, string field)
        {
            foreach (var node in Nodes(root))
            {
                string name = node is StartQuestEffect ? "start_quest" : node is QuestLogEffect ? "quest_log" : node is RevealClueEffect ? "reveal_clue" : null;
                if (name != null) yield return (name, Value(Value(node, "arguments"), "Text") as string[] ?? Array.Empty<string>(), path, field);
            }
        }
        static IEnumerable<object> Nodes(object root)
        {
            var pending = new Stack<object>(); var seen = new HashSet<object>(new Identity()); pending.Push(root);
            while (pending.Count > 0)
            {
                object node = pending.Pop();
                if (node == null || node is string || node is UnityEngine.Object || node.GetType().IsPrimitive || node.GetType().IsEnum || !seen.Add(node)) continue;
                yield return node;
                if (node is IEnumerable array) foreach (object child in array) pending.Push(child);
                else foreach (var member in SerializedFields(node.GetType())) pending.Push(member.GetValue(node));
            }
        }
        sealed class Identity : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
        internal static HashSet<string> Tags(NarrativeValidationContext context)
        {
            var tags = new HashSet<string>(StringComparer.Ordinal);
            foreach (var body in context.Content.All<BodyTemplateDefinition>()) if (!string.IsNullOrEmpty(body.StoryTag)) tags.Add(body.StoryTag);
            foreach (var config in context.Content.All<NarrativeConfigDefinition>())
                if (config.KnownBodyTags != null) foreach (string tag in config.KnownBodyTags) if (!string.IsNullOrEmpty(tag)) tags.Add(tag);
            return tags;
        }
        internal static void Tag(string tag, string field, string path, HashSet<string> tags, ValidationReport report, bool optional = true)
        { if (!(optional && string.IsNullOrEmpty(tag)) && (tag == null || !tags.Contains(tag))) report.Error("QST-V003", path, field, "Unknown body tag '" + tag + "'."); }
        internal static T Resolve<T>(NarrativeValidationContext context, string name) where T : ContentDefinition
            => context.Content.TryGet<T>(new ContentId(name), out var definition) ? definition : null;
    }
}
