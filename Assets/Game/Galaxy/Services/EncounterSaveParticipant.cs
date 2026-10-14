using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Galaxy.Encounters;

namespace Game.Galaxy.Services
{
    /// <summary>ENCT v1: canonical pending story encounters, lossless unknown identities and atomic restore.</summary>
    public sealed class EncounterSaveParticipant : ISaveParticipant
    {
        readonly EncounterSaveState state;
        readonly ContentCatalog catalog;
        public EncounterSaveParticipant(EncounterDirectorService director, ContentCatalog catalog)
            : this((director ?? throw new ArgumentNullException(nameof(director))).SaveState, catalog) { }
        internal EncounterSaveParticipant(EncounterSaveState state, ContentCatalog catalog)
        { this.state = state ?? throw new ArgumentNullException(nameof(state)); this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }
        public string Key => "ENCT";
        public int Version => 1;
        public void Capture(SaveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.WriteUInt32(state.NextInstanceSerial);
            var used = new List<ContentId>(state.GloballyUsed);
            used.Sort((a,b) => StringComparer.Ordinal.Compare(a.Value,b.Value));
            writer.WriteVarUInt32((uint)used.Count); foreach (var id in used) writer.WriteContentId(id);
            var pending = new List<PendingEncounter>(state.Pending);
            foreach (var item in state.Active)
                if (item.Persistent && item.State == InstanceState.Active)
                    pending.Add(new PendingEncounter { Encounter = item.Definition.Id, InstanceId = item.Id, StarSystem = item.StarSystem,
                        Anchor = item.Anchor, Combat = item.Source == EncounterSource.Combat });
            pending.Sort((a,b) => a.InstanceId.CompareTo(b.InstanceId));
            writer.WriteVarUInt32((uint)pending.Count);
            foreach (var item in pending)
            {
                writer.WriteContentId(item.Encounter); writer.WriteUInt32(item.InstanceId); writer.WriteUInt32(item.StarSystem.Value);
                writer.WriteByte((byte)item.Anchor.Kind); writer.WriteUInt64(item.Anchor.Body.LocalKey);
                writer.WriteString(item.Anchor.Tag ?? ""); writer.WriteBool(item.Combat);
            }
        }
        public void Restore(SaveReader reader, int fromVersion)
        {
            if (fromVersion == 0) { state.Reset(SessionResetReason.Cleared); return; }
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (fromVersion != Version) throw new SaveFormatException("Unsupported ENCT version " + fromVersion, reader.Position);
            uint serial = reader.ReadUInt32();
            if (serial == 0) throw new SaveFormatException("Invalid ENCT next instance serial", reader.Position);
            uint count = reader.ReadVarUInt32();
            if (count > reader.Remaining) throw new SaveFormatException("Invalid ENCT used count", reader.Position);
            var used = new HashSet<ContentId>();
            for (uint i = 0; i < count; i++)
            {
                var id = reader.ReadContentId();
                if (id.IsNone || !used.Add(id)) throw new SaveFormatException("Empty or duplicate ENCT used ID", reader.Position);
            }
            count = reader.ReadVarUInt32();
            // Each pending row has at least 19 fixed/length bytes, even with an empty ID/tag.
            if (count > reader.Remaining / 19) throw new SaveFormatException("Invalid ENCT pending count", reader.Position);
            var pending = new List<PendingEncounter>((int)count); var seen = new HashSet<uint>();
            for (uint i = 0; i < count; i++)
            {
                var id = reader.ReadContentId(); uint instance = reader.ReadUInt32(); var system = new StarSystemId(reader.ReadUInt32());
                byte kind = reader.ReadByte(); ulong key = reader.ReadUInt64(); string tag = reader.ReadString(); bool combat = reader.ReadBool();
                var body = BodyId.FromLocalKey(system, key);
                if (id.IsNone || instance == 0 || instance >= serial || !seen.Add(instance) || kind > (byte)EncounterAnchorKind.Tag ||
                    body.LocalKey != key || kind == (byte)EncounterAnchorKind.Body && (!body.IsValid || !Enum.IsDefined(typeof(BodyRole), body.Role)))
                    throw new SaveFormatException("Invalid or duplicate ENCT pending identity", reader.Position);
                pending.Add(new PendingEncounter { Encounter = id, InstanceId = instance, StarSystem = system,
                    Anchor = new EncounterAnchor { Kind = (EncounterAnchorKind)kind, Body = body, Tag = tag }, Combat = combat });
            }
            if (!reader.IsAtEnd) throw new SaveFormatException("Trailing ENCT data", reader.Position);
            // Complete parsing before changing any director-owned state or publishing unresolved reports.
            state.Reset(SessionResetReason.Cleared); state.NextInstanceSerial = serial;
            foreach (var id in used)
            {
                state.GloballyUsed.Add(id);
                if (catalog.TryResolve<EncounterDefinition>(id, Key, out var def)) state.ResolvedGloballyUsed.Add(def.Id);
                else Warn(id);
            }
            state.Pending.AddRange(pending);
            foreach (var item in pending) if (!catalog.TryResolve<EncounterDefinition>(item.Encounter, Key, out _)) Warn(item.Encounter);
        }
        static void Warn(ContentId id) => GameLog.WarnOnce("ENCT.Unknown." + id.Value, "Encounters", "Preserving unknown saved encounter '" + id.Value + "'.");
    }
}
