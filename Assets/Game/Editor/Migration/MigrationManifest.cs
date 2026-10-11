using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Editor.Migration
{
    /// <summary>SPEC-72: deterministic hashes of migration-owned asset bytes. Invalid manifests fail closed.</summary>
    public sealed class MigrationManifest
    {
        readonly SortedDictionary<string, ulong> hashes = new(StringComparer.Ordinal);
        public static MigrationManifest Load(string path)
        {
            var result = new MigrationManifest();
            if (!File.Exists(path)) return result;
            var obj = JObject.Parse(File.ReadAllText(path), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            foreach (var p in obj.Properties())
            {
                string value = p.Value.Type == JTokenType.String ? (string)p.Value : null;
                if (value == null || value.Length != 16 || !ulong.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong hash))
                    throw new InvalidDataException("Invalid migration hash for " + p.Name);
                result.SetHash(p.Name, hash);
            }
            return result;
        }
        public bool TryGetHash(string assetPath, out ulong hash) => hashes.TryGetValue(assetPath, out hash);
        public void SetHash(string assetPath, ulong hash) => hashes[assetPath] = hash;
        public void Save(string path)
        {
            var obj = new JObject();
            foreach (var p in hashes) obj.Add(p.Key, p.Value.ToString("x16", CultureInfo.InvariantCulture));
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, obj.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
        }
    }
}
