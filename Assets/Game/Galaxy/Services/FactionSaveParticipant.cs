using Game.Core.Save;
using Game.Data.Services;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-33 §4.5/5.3 FACN save contract; ENC-004 implements capture/restore (SPEC-05 order 30).</summary>
    public sealed class FactionSaveParticipant : ISaveParticipant
    {
        public FactionSaveParticipant(FactionService factions, ContentCatalog catalog) => throw new System.NotImplementedException("ENC-004");
        public string Key => "FACN";
        public int Version => 1;
        // TODO(ENC-004): canonical sorted relations and unresolved-ID preservation.
        public void Capture(SaveWriter writer) => throw new System.NotImplementedException("ENC-004");
        public void Restore(SaveReader reader, int fromVersion) => throw new System.NotImplementedException("ENC-004");
    }
}
