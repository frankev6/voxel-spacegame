using System;
using System.Collections.Generic;
using Game.Data.Validation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Migration
{
    /// <summary>Editor-only source references for migration. Source art can move without changing converter code (SPEC-72 Q14).</summary>
    internal sealed class LegacyMigrationSources : ScriptableObject
    {
        internal const string AssetPath = "Assets/Game/Editor/Migration/legacy_migration_sources.asset";
        [SerializeField] Texture2D itemIcons;
        internal Dictionary<string, Sprite> LoadIcons(ValidationReport report)
        {
            var icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            string path = AssetDatabase.GetAssetPath(itemIcons);
            if (string.IsNullOrEmpty(path)) { report.Error("MIG-E004", AssetPath, "itemIcons", "Migration icon sheet is not assigned."); return icons; }
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite)
                {
                    if (!icons.TryAdd(sprite.name, sprite)) report.Error("MIG-E004", path, sprite.name, "Ambiguous icon sprite name.");
                }
            return icons;
        }
    }
}
