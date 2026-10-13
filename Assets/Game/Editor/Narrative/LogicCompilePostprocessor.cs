using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-40 NAR-009: coalesced import compilation, source/version write guard and Play deferral.</summary>
    [InitializeOnLoad]
    public sealed class LogicCompilePostprocessor : AssetPostprocessor
    {
        [StaticStateOk("Editor queue; cleared before assembly reload, retained across Play for deferred imports.")]
        static readonly SortedSet<string> pending = new SortedSet<string>(StringComparer.Ordinal);
        [StaticStateOk("Editor diagnostic cache; invalidated by serialized source/version stamps and cleared before reload.")]
        static readonly Dictionary<string, (ulong Stamp, ValidationReport Report)> diagnostics = new();
        [StaticStateOk("Editor delay callback registration, cleared before assembly reload.")]
        static bool scheduled;

        static LogicCompilePostprocessor()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }
        static bool Eligible(string path) => path != null && path.StartsWith(ContentDatabaseBuilder.ContentRoot + "/", StringComparison.Ordinal)
            && path.EndsWith(".asset", StringComparison.Ordinal);
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in deleted.Concat(movedFrom)) { pending.Remove(path); diagnostics.Remove(path); }
            foreach (string path in imported.Concat(moved)) Queue(path);
        }
        internal static void Queue(string path)
        {
            if (!Eligible(path)) return;
            pending.Add(path); Schedule();
        }
        static void Schedule()
        {
            if (scheduled || pending.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;
            scheduled = true; EditorApplication.delayCall += FlushScheduled;
        }
        static void FlushScheduled()
        {
            scheduled = false;
            var report = FlushPending();
            if (report != null && report.Entries.Count != 0) ContentValidatorRunner.Log(report);
        }
        static void OnPlayMode(PlayModeStateChange change)
        { if (change == PlayModeStateChange.EnteredEditMode) Schedule(); }
        static void Clear()
        {
            EditorApplication.delayCall -= FlushScheduled;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            pending.Clear(); diagnostics.Clear(); scheduled = false;
        }
        internal static ValidationReport FlushPending()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || pending.Count == 0) return null;
            string[] paths = pending.ToArray(); pending.Clear();
            // Rebuild first: the content database's independent import callback may run after this one.
            return CompilePaths(ContentDatabaseBuilder.Rebuild(), paths);
        }
        internal static ValidationReport RecompileAll(ContentDatabase database, string root = ContentDatabaseBuilder.ContentRoot)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { root })) Queue(AssetDatabase.GUIDToAssetPath(guid));
                return null;
            }
            return CompilePaths(database, AssetDatabase.FindAssets("t:ScriptableObject", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(x => x, StringComparer.Ordinal));
        }
        static LogicCompiler BuildCompiler(ContentDatabase database, ValidationReport report)
        {
            if (database == null) throw new InvalidOperationException("ContentDatabase is unavailable.");
            var catalog = ContentCatalog.Build(database.Definitions, report);
            var refs = RefResolverRegistry.CreateDefault(catalog);
            return new LogicCompiler(FactCatalog.Build(catalog, refs, report), refs, new LogicFunctionRegistry());
        }
        static ValidationReport CompilePaths(ContentDatabase database, IEnumerable<string> paths)
        {
            var report = new ValidationReport("Logic compilation");
            LogicCompiler compiler = null;
            foreach (string path in paths)
            {
                if (!Eligible(path)) continue;
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!(asset is ILogicSourceOwner owner)) continue;
                var before = Stamps(asset);
                if (!before.Any(x => x.Stale)) continue;
                compiler ??= BuildCompiler(database, report);
                var local = new ValidationReport(path);
                try
                {
                    owner.CompileLogic(compiler, local.For(path));
                    var after = Stamps(asset);
                    // Successful and failed compiles both stamp their source; only stamp changes may trigger reimport.
                    if (!StoredEqual(before, after))
                    { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
                    foreach (var stamp in after.Where(x => x.Stale))
                        local.Error("NAR-E017", path, stamp.Path, "Compiled logic is stale; owner did not compile this field.");
                    diagnostics[path] = (Signature(after), local);
                }
                catch (Exception e)
                {
                    local.Error("NAR-E017", path, "", "Logic owner could not compile: " + e.GetType().Name + ": " + e.Message);
                    diagnostics[path] = (Signature(Stamps(asset)), local);
                }
                report.Merge(local);
            }
            return report;
        }
        internal static string FirstError(SerializedProperty property)
        {
            var asset = property.serializedObject.targetObject;
            string path = AssetDatabase.GetAssetPath(asset);
            if (!(asset is ILogicSourceOwner) || string.IsNullOrEmpty(path)) return "Compilation failed; recompile for diagnostics.";
            ulong signature = Signature(Stamps(asset));
            if (!diagnostics.TryGetValue(path, out var cached) || cached.Stamp != signature)
            {
                // Diagnostics are not stored in the runtime wrapper. Recreate them on a disposable copy after reload.
                var local = new ValidationReport(path); Object copy = null;
                try
                {
                    var compiler = BuildCompiler(AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentDatabaseBuilder.DatabasePath), local);
                    copy = Object.Instantiate(asset); ((ILogicSourceOwner)copy).CompileLogic(compiler, local.For(path));
                }
                catch (Exception e) { local.Error("NAR-E017", path, "", "Logic owner could not compile: " + e.Message); }
                finally { if (copy != null) Object.DestroyImmediate(copy); }
                cached = (signature, local); diagnostics[path] = cached;
            }
            string field = property.propertyPath.Replace(".Array.data[", "[");
            foreach (var entry in cached.Report.Entries)
                if (entry.Severity == ValidationSeverity.Error && entry.AssetPath == path &&
                    (entry.Field == field || entry.Field.StartsWith(field + " (", StringComparison.Ordinal) ||
                     entry.Field.StartsWith(field + "[", StringComparison.Ordinal)))
                    return entry.Code + " " + entry.Field + ": " + entry.Message.Split('\n')[0];
            foreach (var entry in cached.Report.Entries)
                if (entry.Severity == ValidationSeverity.Error) return entry.Code + ": " + entry.Message.Split('\n')[0];
            return "Compilation failed; recompile for diagnostics.";
        }
        readonly struct Stamp
        {
            internal readonly string Path;
            internal readonly ulong SourceHash, TextHash;
            internal readonly int Version;
            internal bool Stale => SourceHash != TextHash || Version != LogicCompiler.Version;
            internal Stamp(SerializedProperty property)
            {
                Path = property.propertyPath; SourceHash = property.FindPropertyRelative("sourceHash").ulongValue;
                Version = property.FindPropertyRelative("compilerVersion").intValue;
                TextHash = StableHash.Hash64(SourceText(property.FindPropertyRelative("source")));
            }
        }
        internal static string SourceText(SerializedProperty source)
        {
            if (!source.isArray || source.propertyType == SerializedPropertyType.String) return source.stringValue ?? "";
            var lines = new string[source.arraySize];
            for (int i = 0; i < lines.Length; i++) lines[i] = source.GetArrayElementAtIndex(i).stringValue;
            return string.Join("\n", lines);
        }
        internal static bool IsStale(SerializedProperty property) => new Stamp(property).Stale;
        static List<Stamp> Stamps(Object asset)
        {
            var result = new List<Stamp>(); using var serialized = new SerializedObject(asset);
            var property = serialized.GetIterator(); bool children = true;
            while (property.Next(children))
            {
                children = true;
                if (property.type != nameof(CompiledCondition) && property.type != nameof(CompiledIntExpr) && property.type != nameof(CompiledEffectList)) continue;
                result.Add(new Stamp(property)); children = false;
            }
            return result;
        }
        static bool StoredEqual(List<Stamp> a, List<Stamp> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i].Path != b[i].Path || a[i].SourceHash != b[i].SourceHash || a[i].Version != b[i].Version) return false;
            return true;
        }
        static ulong Signature(List<Stamp> stamps)
        {
            ulong hash = 0;
            foreach (var stamp in stamps)
                hash = StableHash.Combine(StableHash.Combine(hash, stamp.TextHash), StableHash.Combine(stamp.SourceHash, (ulong)(uint)stamp.Version));
            return hash;
        }
    }
}
