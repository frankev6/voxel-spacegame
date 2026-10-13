using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;

namespace Game.Simulation.Factions
{
    /// <summary>SPEC-33 §4.2 deterministic faction-index surface; ENC-003 supplies the immutable map.</summary>
    public sealed class FactionIndexMap
    {
        // TODO(ENC-003): player first, then ordinal catalog order; 255 for none/unknown.
        public static FactionIndexMap Build(ContentCatalog catalog) => throw new System.NotImplementedException("ENC-003");
        public int Count => throw new System.NotImplementedException("ENC-003");
        public byte IndexOf(FactionDefinition def) => throw new System.NotImplementedException("ENC-003");
        public byte IndexOf(in ContentHash faction) => throw new System.NotImplementedException("ENC-003");
        public FactionDefinition ByIndex(byte index) => throw new System.NotImplementedException("ENC-003");
    }
}
