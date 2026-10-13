using Game.Core.Save;
using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    /// <summary>Implements SPEC-31 §4.4/5.10. PLOC contract; SYS-009 owns binary capture/restore.</summary>
    public sealed class PlayerLocationSaveParticipant : ISaveParticipant
    {
        public PlayerLocationSaveParticipant(GalaxyState state, DockingService docking) => throw new System.NotImplementedException("SYS-009");
        public string Key => "PLOC";
        public int Version => 1;
        public void Capture(SaveWriter writer) => throw new System.NotImplementedException("SYS-009");
        public void Restore(SaveReader reader, int fromVersion) => throw new System.NotImplementedException("SYS-009");
    }
}
