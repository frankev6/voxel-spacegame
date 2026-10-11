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
            return database;
        }
    }
}
