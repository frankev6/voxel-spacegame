using System.Collections.Generic;

namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. Every JSON file under Import/Legacy/content, indexed by $path and $type.</summary>
    public sealed class LegacyContentIndex
    {
        public static LegacyContentIndex Load(string legacyRoot, Game.Data.Validation.ValidationReport report) { throw new System.NotImplementedException("MIG-002"); }
        public IReadOnlyList<LegacyFile> Files => throw new System.NotImplementedException("MIG-002");        // sorted by RelativePath (ordinal)
        public bool TryGetByAssetPath(string assetPath, out LegacyFile file) { throw new System.NotImplementedException("MIG-002"); }
        public IReadOnlyList<LegacyFile> OfType(string legacyType) { throw new System.NotImplementedException("MIG-002"); } // exact $type, sorted by RelativePath
        public bool TryResolve(LegacyRef reference, out LegacyFile file) { throw new System.NotImplementedException("MIG-002"); }                // IsNull → false without report
    }
}
