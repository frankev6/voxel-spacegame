using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2 and §5.1 step 2. Every JSON file under Import/Legacy/content, indexed by $path and $type.</summary>
    public sealed class LegacyContentIndex
    {
        static readonly IReadOnlyList<LegacyFile> NoFiles = Array.Empty<LegacyFile>();

        readonly List<LegacyFile> files = new List<LegacyFile>();
        readonly Dictionary<string, LegacyFile> byAssetPath = new Dictionary<string, LegacyFile>(StringComparer.Ordinal);
        readonly Dictionary<string, List<LegacyFile>> byType = new Dictionary<string, List<LegacyFile>>(StringComparer.Ordinal);

        LegacyContentIndex() { }

        /// <summary>Parses every content/**/*.json (ordinal order of relative path) as a JObject. Unreadable files and files without
        /// $path or $type are reported as MIG-E002 and left out.</summary>
        public static LegacyContentIndex Load(string legacyRoot, Game.Data.Validation.ValidationReport report)
        {
            var index = new LegacyContentIndex();
            string contentDir = Path.Combine(legacyRoot, "content");
            if (!Directory.Exists(contentDir)) { report.Error("MIG-E001", contentDir, "", "The legacy content folder does not exist."); return index; }
            var paths = new List<string>();
            foreach (var p in Directory.GetFiles(contentDir, "*.json", SearchOption.AllDirectories))
                paths.Add(Path.GetRelativePath(legacyRoot, p).Replace('\\', '/'));
            paths.Sort(StringComparer.Ordinal);
            foreach (var rel in paths)
            {
                JObject json;
                try { json = LegacyJson.ParseObject(File.ReadAllText(Path.Combine(legacyRoot, rel))); }
                catch (JsonException e) { report.Error("MIG-E002", rel, "", "The file is not readable JSON: " + e.Message); continue; }
                string assetPath = (string)json["$path"], type = (string)json["$type"];
                if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(type))
                { report.Error("MIG-E002", rel, string.IsNullOrEmpty(assetPath) ? "$path" : "$type", "The file has no $path or $type."); continue; }
                var file = new LegacyFile { RelativePath = rel, AssetPath = assetPath, Type = type, Name = (string)json["$name"], Json = json };
                if (!index.byAssetPath.TryAdd(assetPath, file))
                { report.Error("MIG-E002", rel, "$path", $"$path {assetPath} is also used by {index.byAssetPath[assetPath].RelativePath}."); continue; }
                index.files.Add(file);
                if (!index.byType.TryGetValue(type, out var list)) index.byType[type] = list = new List<LegacyFile>();
                list.Add(file);
            }
            return index;
        }

        public IReadOnlyList<LegacyFile> Files => files;                                   // sorted by RelativePath (ordinal)
        public bool TryGetByAssetPath(string assetPath, out LegacyFile file)
        { file = null; return assetPath != null && byAssetPath.TryGetValue(assetPath, out file); }
        public IReadOnlyList<LegacyFile> OfType(string legacyType)                         // exact $type, sorted by RelativePath
            => legacyType != null && byType.TryGetValue(legacyType, out var list) ? list : NoFiles;
        public bool TryResolve(LegacyRef reference, out LegacyFile file)                   // IsNull → false without report
        { file = null; return !reference.IsNull && byAssetPath.TryGetValue(reference.Path, out file); }
    }
}
