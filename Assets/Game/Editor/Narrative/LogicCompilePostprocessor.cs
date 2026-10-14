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
                if (!(asset is ILogicSourceOwner)) continue;
                var before = Stamps(asset);
                if (before.Count == 0) continue;
                compiler ??= BuildCompiler(database, report);
                var local = new ValidationReport(path);
                Object copy = null;
                try
                {
                    // Owners compile every wrapper. Compile a copy so current wrappers retain their live
                    // graph objects and SerializeReference IDs, even beside a changed field or on an error.
                    copy = Object.Instantiate(asset);
                    ((ILogicSourceOwner)copy).CompileLogic(compiler, local.For(path));
                    bool changed = CopyChangedWrappers(asset, copy, before, Stamps(copy));
                    var after = Stamps(asset);
                    // Successful and failed compiles both stamp their source; only stamp changes may trigger reimport.
                    if (changed)
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
                finally { if (copy != null) Object.DestroyImmediate(copy); }
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
                if (property.isArray) continue; // Visit elements, not the collection container.
                if (property.type != nameof(CompiledCondition) && property.type != nameof(CompiledIntExpr) && property.type != nameof(CompiledEffectList)) continue;
                result.Add(new Stamp(property)); children = false;
            }
            return result;
        }
        static bool CopyChangedWrappers(Object asset, Object copy, List<Stamp> before, List<Stamp> compiled)
        {
            using var target = new SerializedObject(asset);
            using var source = new SerializedObject(copy);
            bool changed = false;
            foreach (var stamp in compiled)
            {
                int index = before.FindIndex(old => old.Path == stamp.Path);
                if (index >= 0 && before[index].SourceHash == stamp.SourceHash && before[index].Version == stamp.Version) continue;
                using var property = source.FindProperty(stamp.Path);
                using var destination = target.FindProperty(stamp.Path);
                // Generic wrapper properties do not copy their children. Transfer the serialized
                // graph explicitly; current wrappers never enter this path and retain their IDs.
                using var root = property.FindPropertyRelative("root");
                if (root != null)
                    destination.FindPropertyRelative("root").managedReferenceValue = root.managedReferenceValue;
                else
                {
                    using var effects = property.FindPropertyRelative("effects");
                    using var into = destination.FindPropertyRelative("effects");
                    into.arraySize = effects.arraySize;
                    for (int i = 0; i < effects.arraySize; i++)
                        into.GetArrayElementAtIndex(i).managedReferenceValue = effects.GetArrayElementAtIndex(i).managedReferenceValue;
                }
                destination.FindPropertyRelative("sourceHash").ulongValue = stamp.SourceHash;
                destination.FindPropertyRelative("compilerVersion").intValue = stamp.Version;
                destination.FindPropertyRelative("hasErrors").boolValue = property.FindPropertyRelative("hasErrors").boolValue;
                changed = true;
            }
            if (changed) target.ApplyModifiedPropertiesWithoutUndo();
            return changed;
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
