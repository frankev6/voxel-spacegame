namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. One legacy JSON file.</summary>
    public sealed class LegacyFile
    {
        public string RelativePath;     // e.g. "content/Items/Blocks/Hulls/block_hull.json"
        public string AssetPath;        // "$path"
        public string Type;             // "$type"
        public string Name;             // "$name"
        public Newtonsoft.Json.Linq.JObject Json;
    }
}
