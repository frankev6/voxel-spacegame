namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. Hashes of the assets the migration wrote (Import/Migration/manifest.json).</summary>
    public sealed class MigrationManifest
    {
        public static MigrationManifest Load(string path) { throw new System.NotImplementedException("MIG-004"); }               // missing file → empty
        public bool TryGetHash(string assetPath, out ulong hash) { throw new System.NotImplementedException("MIG-004"); }
        public void SetHash(string assetPath, ulong hash) { throw new System.NotImplementedException("MIG-004"); }
        public void Save(string path) { throw new System.NotImplementedException("MIG-004"); }                                   // JSON object sorted by key, "x16" hex values, LF line endings
    }
}
