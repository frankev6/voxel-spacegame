using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Validation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.Migration
{
    /// <summary>SPEC-72: updates only migration-owned bytes, preserving GUIDs and hand edits.</summary>
    public sealed class LegacyAssetWriter
    {
        readonly string root;
        readonly MigrationManifest manifest;
        readonly bool force;
        readonly ValidationReport report;
        readonly List<(string AssetPath, WriteOutcome Outcome)> outcomes = new();
        string fillingPath;
        public IReadOnlyList<(string AssetPath, WriteOutcome Outcome)> Outcomes => outcomes;
        public LegacyAssetWriter(string contentRoot, MigrationManifest manifest, bool force, ValidationReport report)
        {
            root = contentRoot?.Replace('\\', '/').TrimEnd('/') ?? throw new ArgumentNullException(nameof(contentRoot));
            if (!root.StartsWith("Assets/", StringComparison.Ordinal) || root.Split('/').Any(p => p == ".." || p == "." || p.Length == 0))
                throw new ArgumentException("Content root must be a project Assets subfolder.", nameof(contentRoot));
            this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            this.force = force; this.report = report ?? throw new ArgumentNullException(nameof(report));
            CheckPath(root + "/probe.asset");
        }
        public T CreateOrUpdate<T>(string area, string id, Action<SerializedObject> fill, out WriteOutcome outcome) where T : ContentDefinition
        {
            if (string.IsNullOrEmpty(id) || id.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || id == "." || id == "..") throw new ArgumentException("Invalid asset ID.", nameof(id));
            string path = root + "/" + area + "/" + id + ".asset";
            CheckPath(path);
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if ((existing != null && existing.GetType() != typeof(T)) || (existing == null && File.Exists(path)))
            {
                report.Error("MIG-E010", path, "", "Asset at target path has another type.");
                Record(path, outcome = WriteOutcome.Failed); return null;
            }
            if (existing != null && !force && !Owned(path, existing))
            {
                report.Info("MIG-I011", path, "", "Kept hand-edited asset.");
                Record(path, outcome = WriteOutcome.SkippedHandEdited); return (T)existing;
            }
            T tmp = ScriptableObject.CreateInstance<T>();
            try
            {
                tmp.name = id;
                using (var so = new SerializedObject(tmp))
                {
                    so.FindProperty("id").stringValue = id;
                    string previousPath = fillingPath;
                    try { fillingPath = path; fill?.Invoke(so); }
                    finally { fillingPath = previousPath; }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (existing == null)
                {
                    string directory = Path.GetDirectoryName(path);
                    if (!AssetDatabase.IsValidFolder(directory)) { Directory.CreateDirectory(directory); AssetDatabase.Refresh(); }
                    AssetDatabase.CreateAsset(tmp, path);
                    existing = tmp; tmp = null; outcome = WriteOutcome.Created;
                }
                else
                {
                    byte[] before = File.ReadAllBytes(path);
                    EditorUtility.CopySerialized(tmp, existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                    outcome = before.SequenceEqual(File.ReadAllBytes(path)) ? WriteOutcome.Unchanged : WriteOutcome.Updated;
                }
                manifest.SetHash(path, Hash(path));
                Record(path, outcome);
                return (T)existing;
            }
            finally { if (tmp != null) Object.DestroyImmediate(tmp); }
        }
        // SerializedObject is sealed: converter fills use this helper to skip unavailable target fields.
        internal void Set(SerializedObject so, string propertyPath, Action<SerializedProperty> fill)
        {
            var property = Find(so, propertyPath);
            if (property != null) fill(property);
        }
        SerializedProperty Find(SerializedObject so, string propertyPath)
        {
            var property = so.FindProperty(propertyPath);
            if (property == null) report.Warning("MIG-W090", fillingPath ?? AssetDatabase.GetAssetPath(so.targetObject), propertyPath, "Field missing in the target type.");
            return property;
        }
        public bool SetLinkIfEmpty(ContentDefinition asset, string propertyPath, Object value)
        {
            string path = AssetDatabase.GetAssetPath(asset); CheckPath(path);
            using var so = new SerializedObject(asset);
            var p = Find(so, propertyPath); if (p == null) return false;
            if (p.propertyType != SerializedPropertyType.ObjectReference) throw new ArgumentException("Expected object reference.", nameof(propertyPath));
            if (p.objectReferenceValue == value) return false;
            if (p.objectReferenceValue != null) { KeptLink(path, propertyPath); return false; }
            bool owned = Owned(path, asset);
            p.objectReferenceValue = value; SaveLink(so, asset, path, owned); return true;
        }
        public bool SetLinkArrayIfEmpty(ContentDefinition asset, string propertyPath, IReadOnlyList<Object> values)
        {
            string path = AssetDatabase.GetAssetPath(asset); CheckPath(path);
            using var so = new SerializedObject(asset);
            var p = Find(so, propertyPath); if (p == null) return false;
            if (!p.isArray || p.propertyType == SerializedPropertyType.String) throw new ArgumentException("Expected reference array.", nameof(propertyPath));
            if (p.arraySize != 0) { KeptLink(path, propertyPath); return false; }
            if (values == null || values.Count == 0) return false;
            bool owned = Owned(path, asset);
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            SaveLink(so, asset, path, owned); return true;
        }
        void SaveLink(SerializedObject so, ContentDefinition asset, string path, bool owned)
        {
            so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(asset);
            // Filling a hand-edited asset's empty link must never bless all its other fields for overwrite.
            if (owned) manifest.SetHash(path, Hash(path));
            Record(path, WriteOutcome.Updated);
        }
        void KeptLink(string path, string field) => report.Info("MIG-I012", path, field, "Kept existing link.");
        bool Owned(string path, Object asset) => !EditorUtility.IsDirty(asset) && manifest.TryGetHash(path, out ulong expected) && File.Exists(path) && expected == Hash(path);
        static ulong Hash(string path) => StableHash.Hash64(File.ReadAllBytes(path));
        void Record(string path, WriteOutcome outcome) => outcomes.Add((path, outcome));
        void CheckPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.Split('/').Any(p => p == ".." || p == "." || p.Length == 0) || !path.StartsWith(root + "/", StringComparison.Ordinal))
                throw new ArgumentException("Migration writes must stay inside the content root.", nameof(path));
            for (var d = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))); d != null; d = d.Parent)
                if (d.Exists && (d.Attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Migration target uses a symbolic link.", nameof(path));
        }
    }
}
