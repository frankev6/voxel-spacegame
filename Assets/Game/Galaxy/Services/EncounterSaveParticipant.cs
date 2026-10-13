using Game.Core.Save;
using Game.Data.Services;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-33 §4.5/5.10 ENCT save contract; ENC-008 implements pending instances (SPEC-05 order 100).</summary>
    public sealed class EncounterSaveParticipant : ISaveParticipant
    {
        public EncounterSaveParticipant(EncounterDirectorService director, ContentCatalog catalog) => throw new System.NotImplementedException("ENC-008");
        public string Key => "ENCT";
        public int Version => 1;
        // TODO(ENC-008): canonical instance serials, globally used IDs and pending entries.
        public void Capture(SaveWriter writer) => throw new System.NotImplementedException("ENC-008");
        public void Restore(SaveReader reader, int fromVersion) => throw new System.NotImplementedException("ENC-008");
    }
}
