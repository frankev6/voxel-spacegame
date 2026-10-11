using System.Collections.Generic;

namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2 (rows in §4.4).</summary>
    public sealed class LegacyIdMap
    {
        public static LegacyIdMap Default => throw new System.NotImplementedException("MIG-003");                                         // §4.4, immutable
        public IReadOnlyList<LegacyIdMapping> All => throw new System.NotImplementedException("MIG-003");
        public bool TryMap(LegacyIdKind kind, string legacyKey, out string v2Id) { throw new System.NotImplementedException("MIG-003"); }         // false if the key is unknown; true with null for "not converted"
        /// <summary>For narrative DSL tokens: tries Item, Block, Ship in that order; accepts numeric legacy IDs.</summary>
        public bool TryMapItemToken(string token, out string v2Id, out LegacyIdKind kind) { throw new System.NotImplementedException("MIG-003"); }
    }
}
