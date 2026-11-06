using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using Game.Data.Definitions;
using UnityEditor;

namespace Game.Editor.Content
{
    /// <summary>Keeps Assets/Game/Content/ContentDatabase.asset equal to every definition under Assets/Game/Content, sorted by ID (SPEC-04 §5.7).</summary>
    public static class ContentDatabaseBuilder
    {
        public const string ContentRoot = "Assets/Game/Content";
        public const string DatabasePath = ContentRoot + "/ContentDatabase.asset";
        // Trust incremental membership only after a full scan in this editor domain. Reloads,
        // database replacement and external list edits fall back to the canonical scan.
        static ContentDatabase scannedDatabase;
        static ContentDefinition[] scannedDefinitions;

        /// <summary>Loads every definition under the content root, sorts by ordinal ID (empty IDs last, then by path) and updates the database
        /// only if the list changed. Creates the database if it is missing.</summary>
        public static ContentDatabase Rebuild()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath);
            if (database == null)
            {
                Directory.CreateDirectory(ContentRoot);
                database = UnityEngine.ScriptableObject.CreateInstance<ContentDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }
            var found = AssetDatabase.FindAssets("t:ContentDefinition", new[] { ContentRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<ContentDefinition>().Select(d => (Def: d, Path: p)))
                .ToList();
            Apply(database, found);
            return database;
        }

        internal static ContentDatabase RebuildImported(IReadOnlyCollection<string> imported)
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath);
            if (database == null || database != scannedDatabase ||
                scannedDefinitions == null || !database.Definitions.SequenceEqual(scannedDefinitions)) return Rebuild();
            var paths = new HashSet<string>(imported, System.StringComparer.Ordinal);
            var found = new List<(ContentDefinition Def, string Path)>(database.Definitions.Count);
            foreach (var definition in database.Definitions)
            {
                // A removed subasset may already be a destroyed Unity object. Its former path
                // is unavailable, so reconstruct instead of silently dropping unknown content.
                if (definition == null) return Rebuild();
                string path = AssetDatabase.GetAssetPath(definition);
                if (string.IsNullOrEmpty(path) || !path.StartsWith(ContentRoot + "/", System.StringComparison.Ordinal)) return Rebuild();
                if (!paths.Contains(path)) found.Add((definition, path));
            }
            foreach (string path in paths)
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is ContentDefinition definition) found.Add((definition, path));
            Apply(database, found);
            return database;
        }

        static void Apply(ContentDatabase database, List<(ContentDefinition Def, string Path)> found)
        {
            found.Sort((a, b) =>
            {
                bool ea = string.IsNullOrEmpty(a.Def.IdString), eb = string.IsNullOrEmpty(b.Def.IdString);
                if (ea != eb) return ea ? 1 : -1;
                int c = ea ? 0 : string.CompareOrdinal(a.Def.IdString, b.Def.IdString);
                return c != 0 ? c : string.CompareOrdinal(a.Path, b.Path);
            });
            var sorted = found.Select(f => f.Def).ToList();
            if (!sorted.SequenceEqual(database.Definitions))
            {
                database.EditorSetDefinitions(sorted);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssetIfDirty(database);
            }
            scannedDatabase = database;
            // Own the snapshot: Definitions exposes its underlying list as IReadOnlyList, and
            // serialized/editor changes can mutate that list without replacing its identity.
            scannedDefinitions = database.Definitions.ToArray();
        }
    }
}
