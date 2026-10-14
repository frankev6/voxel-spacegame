using System;
using Game.Core.Save;
using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-31 §5.10: PLOC v1, raw-bit position/yaw and silent docking restore.</summary>
    public sealed class PlayerLocationSaveParticipant : ISaveParticipant
    {
        readonly GalaxyState state;
        readonly DockingService docking;
        public PlayerLocationSaveParticipant(GalaxyState state, DockingService docking)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.docking = docking ?? throw new ArgumentNullException(nameof(docking));
        }
        public string Key => "PLOC";
        public int Version => 1;
        public void Capture(SaveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.WriteUInt32(state.CurrentStarSystem.Value);
            writer.WriteBool(state.HasPlayerLocation);
            writer.WriteUInt32(state.PlayerLocation.StarSystem.Value);
            writer.WriteDouble3(state.PlayerLocation.PositionM);
            writer.WriteSingle(state.PlayerLocation.YawRad);
            writer.WriteBool(docking.IsDocked);
            writer.WriteUInt32(docking.DockedBody.System.Value);
            writer.WriteUInt64(docking.DockedBody.LocalKey);
        }
        public void Restore(SaveReader reader, int fromVersion)
        {
            if (fromVersion == 0)
            {
                state.CurrentStarSystem = default; state.HasPlayerLocation = false;
                state.PlayerLocation = default; docking.RestoreDocked(default); return;
            }
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (fromVersion != Version) throw new SaveFormatException("Unsupported PLOC version " + fromVersion, reader.Position);
            // Read and validate the complete record before changing live state.
            var system = new StarSystemId(reader.ReadUInt32());
            bool hasLocation = reader.ReadBool();
            var location = new PlayerLocation { StarSystem = new StarSystemId(reader.ReadUInt32()),
                PositionM = reader.ReadDouble3(), YawRad = reader.ReadSingle() };
            bool docked = reader.ReadBool();
            var dockSystem = new StarSystemId(reader.ReadUInt32());
            ulong key = reader.ReadUInt64();
            var body = BodyId.FromLocalKey(dockSystem, key);
            if (hasLocation && (!system.IsValid || !location.StarSystem.Equals(system)))
                throw new SaveFormatException("Inconsistent PLOC star system", reader.Position);
            if (docked && (!hasLocation || !body.IsValid || body.LocalKey != key || !dockSystem.Equals(system)))
                throw new SaveFormatException("Invalid PLOC docking identity", reader.Position);
            if (!reader.IsAtEnd) throw new SaveFormatException("Trailing PLOC data", reader.Position);
            state.CurrentStarSystem = system; state.HasPlayerLocation = hasLocation; state.PlayerLocation = location;
            docking.RestoreDocked(docked ? body : default);
        }
    }
}
