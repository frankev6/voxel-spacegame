using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Narrative
{
    /// <summary>One editor validation pass; graphs belong to disposable copies, never authored assets.</summary>
    public sealed class NarrativeValidationContext
    {
        public sealed class CompiledGraph
        {
            public ContentDefinition Owner { get; }
            public string AssetPath { get; }
            public string Field { get; }
            public bool Debug { get; }
            public object Wrapper { get; }
            public object Root { get; }
            internal CompiledGraph(ContentDefinition owner, string path, string field, object wrapper, bool debug)
            {
                Owner = owner; AssetPath = path; Field = field; Wrapper = wrapper; Debug = debug;
                Root = Value(wrapper, wrapper is CompiledEffectList ? "effects" : "root");
            }
        }

        readonly List<CompiledGraph> graphs = new();
        public ContentCatalog Content { get; }
        public FactCatalog Facts { get; }
        public RefResolverRegistry Refs { get; }
        public LogicCompiler Compiler { get; }
        public IReadOnlyList<CompiledGraph> Graphs => graphs;
        public FactUsageAnalyzer Usage { get; }
        public IReadOnlyCollection<string> UnwiredMembers { get; }
        internal NarrativeValidationContext(ContentCatalog content, FactCatalog facts, RefResolverRegistry refs,
            LogicCompiler compiler, IReadOnlyCollection<string> unwired)
        {
            Content = content; Facts = facts; Refs = refs; Compiler = compiler;
            UnwiredMembers = unwired; Usage = new FactUsageAnalyzer(facts, refs);
        }

        internal void AddGraph(ContentDefinition owner, string assetPath, string field, object wrapper, bool debug = false)
        {
            if (!(wrapper is CompiledCondition || wrapper is CompiledIntExpr || wrapper is CompiledEffectList))
                throw new ArgumentException("Expected a compiled logic wrapper.", nameof(wrapper));
            var graph = new CompiledGraph(owner, assetPath ?? "", field ?? "", wrapper, debug);
            graphs.Add(graph); Usage.Analyze(graph);
        }

        internal static string PathOf(ContentDefinition owner)
        {
            string path = AssetDatabase.GetAssetPath(owner);
            return string.IsNullOrEmpty(path) ? "content:" + owner.IdString : path;
        }
        internal static bool IsDebug(ContentDefinition owner)
        {
            if (owner is StoryBeatDefinition beat) return beat.Debug;
            if (owner is QuestDefinition quest) return quest.Kind == QuestKind.Debug;
            // Additional source owners can expose the same explicit debug flag, without a runtime API change.
            var property = owner.GetType().GetProperty("Debug", BindingFlags.Public | BindingFlags.Instance);
            return property?.PropertyType == typeof(bool) && (bool)property.GetValue(owner);
        }
        internal static object Value(object owner, string name)
        {
            if (owner == null) return null;
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(owner);
            }
            return null;
        }
        internal static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            for (; type != null && type != typeof(ScriptableObject) && type != typeof(UnityEngine.Object); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (!field.IsNotSerialized && (field.IsPublic || field.IsDefined(typeof(SerializeField), false) || field.IsDefined(typeof(SerializeReference), false)))
                        yield return field;
        }
        internal static IEnumerable<(string Field, object Wrapper)> Wrappers(ContentDefinition owner)
        {
            var result = new List<(string, object)>();
            var ancestors = new List<object>();
            void Visit(object value, string path, int depth)
            {
                if (value == null || depth > 64) return;
                if (value is CompiledCondition || value is CompiledIntExpr || value is CompiledEffectList)
                { result.Add((path, value)); return; }
                if (value is string || value.GetType().IsPrimitive || value.GetType().IsEnum || value is UnityEngine.Object && depth > 0) return;
                if (ancestors.Exists(x => ReferenceEquals(x, value))) return;
                ancestors.Add(value);
                if (value is IList list)
                    for (int i = 0; i < list.Count; i++) Visit(list[i], path + "[" + i + "]", depth + 1);
                else foreach (var field in SerializedFields(value.GetType()))
                    Visit(field.GetValue(value), path.Length == 0 ? field.Name : path + "." + field.Name, depth + 1);
                ancestors.RemoveAt(ancestors.Count - 1);
            }
            Visit(owner, "", 0);
            result.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
            return result;
        }
    }
}
