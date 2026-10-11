using System;
using Game.Core.Save;

namespace Game.App.Session
{
    /// <summary>SESS v1, order 10: captures managed campaign state and restores the same session (SPEC-01 §5.10).</summary>
    public sealed class SessionSaveParticipant : ISaveParticipant
    {
        readonly GameSession session;
        public SessionSaveParticipant(GameSession session) => this.session = session ?? throw new ArgumentNullException(nameof(session));
        public string Key => "SESS";
        public int Version => 1;
        public void Capture(SaveWriter writer)
        {
            writer.WriteString(session.CommanderName); writer.WriteUInt64(session.CampaignSeed);
            writer.WriteDouble(session.UniversalTime); writer.WriteDouble(session.PlaytimeSeconds);
            writer.WriteUInt64(session.NextPersistentId); writer.WriteBool(session.IsCreativeSession);
        }
        public void Restore(SaveReader reader, int fromVersion)
        {
            if (fromVersion == 0) { session.ResetSavedState(); return; }
            if (fromVersion != Version) throw new SaveFormatException("Unsupported SESS version", reader.Position);
            // Validate the complete record before publishing any field, including discarding an existing ID grant.
            string name = reader.ReadString(); ulong seed = reader.ReadUInt64();
            double time = reader.ReadDouble(), playtime = reader.ReadDouble();
            ulong next = reader.ReadUInt64(); bool creative = reader.ReadBool();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 32 || next == 0 ||
                time < 0 || double.IsNaN(time) || double.IsInfinity(time) ||
                playtime < 0 || double.IsNaN(playtime) || double.IsInfinity(playtime))
                throw new SaveFormatException("Invalid SESS state", reader.Position);
            session.ApplySavedState(name, seed, time, playtime, next, creative);
        }
    }
}
