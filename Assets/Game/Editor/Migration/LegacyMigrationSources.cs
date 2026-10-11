using System;
using System.Collections.Generic;
using System.IO;
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
        [SerializeField] GameObject[] blockModels = Array.Empty<GameObject>();
        internal Dictionary<string, Mesh> LoadMeshes(ValidationReport report)
        {
            var meshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            foreach (var model in blockModels)
            {
                string path = AssetDatabase.GetAssetPath(model);
                if (string.IsNullOrEmpty(path)) { report.Error("MIG-E031", AssetPath, "blockModels", "Missing model source."); continue; }
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Mesh mesh && !meshes.TryAdd(Path.GetFileNameWithoutExtension(path) + "/" + mesh.name, mesh))
                        report.Error("MIG-E031", path, mesh.name, "Ambiguous prop mesh name.");
            }
            return meshes;
        }
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
