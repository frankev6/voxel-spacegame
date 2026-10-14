using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Narrative.Beats;
using Game.Narrative.Clues;
using Game.Narrative.Quests;

namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 version1: canonical named snapshots, validated before replacing any owner state.</summary>
    public sealed class QuestSaveParticipant : ISaveParticipant
    {
        readonly ContentCatalog catalog;
        readonly FactStore facts;
        readonly QuestService quests;
        readonly ClueService clues;
        readonly BeatService beats;
        readonly List<QuestService.SavedRecord> records = new();
        readonly List<ContentId> seen = new(), resolved = new();
        public QuestSaveParticipant(ContentCatalog catalog, FactStore facts, QuestService quests, ClueService clues, BeatService beats)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
            this.quests = quests ?? throw new ArgumentNullException(nameof(quests));
            this.clues = clues ?? throw new ArgumentNullException(nameof(clues));
            this.beats = beats ?? throw new ArgumentNullException(nameof(beats));
        }
        public string Key => "QUST";
        public int Version => 1;
        public void Capture(SaveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            quests.CaptureRecords(records); writer.WriteVarUInt32((uint)records.Count);
            foreach (var record in records)
            {
                var value = record.Value;
                writer.WriteContentId(record.Id); writer.WriteVarUInt32((uint)value.Instance);
                writer.WriteByte((byte)value.Status); writer.WriteVarInt32(value.Stage); writer.WriteUInt64(value.DoneBits);
                writer.WriteVarUInt32((uint)value.AcceptOrder);
                WriteBaseline(writer, value.AcceptBaselineNames, value.AcceptBaselineSlots, value.AcceptBaselineValues);
                WriteBaseline(writer, value.StageBaselineNames, value.StageBaselineSlots, value.StageBaselineValues);
                writer.WriteVarUInt32((uint)value.Log.Count); foreach (string entry in value.Log) writer.WriteString(entry);
            }
            writer.WriteContentId(quests.SavedTracked);
            var perBody = new List<(ContentId Beat, uint System, ulong Key)>(beats.PerBody);
            perBody.Sort((a, b) => { int n = string.CompareOrdinal(a.Beat.Value, b.Beat.Value); if (n == 0) n = a.System.CompareTo(b.System); return n != 0 ? n : a.Key.CompareTo(b.Key); });
            writer.WriteVarUInt32((uint)perBody.Count);
            foreach (var entry in perBody) { writer.WriteContentId(entry.Beat); writer.WriteUInt32(entry.System); writer.WriteUInt64(entry.Key); }
            var definitions = catalog.All<StoryBeatDefinition>();
            writer.WriteVarUInt32((uint)beats.QueueState.Count);
            foreach (var entry in beats.QueueState.Entries)
            {
                writer.WriteContentId(entry.Beat == 0 ? entry.RawId : definitions[entry.Beat - 1].Id);
                var scope = entry.Scope; var body = scope.Body;
                writer.WriteBool(scope.HasBody); writer.WriteUInt32(body.StarSystem); writer.WriteByte(body.Role);
                writer.WriteUInt16(body.Slot); writer.WriteUInt16(body.SubSlot); writer.WriteUInt64(body.Template.Value);
            }
            clues.CaptureState(seen, resolved); WriteIds(writer, seen); WriteIds(writer, resolved);
        }
        void WriteBaseline(SaveWriter writer, string[] names, int[] slots, int[] values)
        {
            int count = values?.Length ?? 0;
            var entries = new List<(string Name, int Value)>(count);
            for (int i = 0; i < count; i++) entries.Add((names == null ? facts.Catalog.IdAt(slots[i]).Name : names[i], values[i]));
            entries.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            writer.WriteVarUInt32((uint)count);
            foreach (var entry in entries) { writer.WriteString(entry.Name); writer.WriteInt32(entry.Value); }
        }
        static void WriteIds(SaveWriter writer, List<ContentId> ids)
        { writer.WriteVarUInt32((uint)ids.Count); foreach (var id in ids) writer.WriteContentId(id); }
        static SaveFormatException Invalid(SaveReader reader, string message) => new("QUST: " + message, reader.Position);
        static int Number(SaveReader reader)
        { uint n = reader.ReadVarUInt32(); if (n > int.MaxValue) throw Invalid(reader, "integer exceeds runtime range"); return (int)n; }
        static int Count(SaveReader reader, int minimumBytes)
        { int n = Number(reader); if (n > reader.Remaining / minimumBytes) throw Invalid(reader, "count exceeds remaining payload"); return n; }
        static string Name(SaveReader reader)
        { string name = reader.ReadString(); if (!ContentIdRules.IsValidFormat(name)) throw Invalid(reader, "invalid name"); return name; }
        static ContentId Id(SaveReader reader, bool allowNone = false)
        {
            var id = reader.ReadContentId();
            if (!(allowNone && id.IsNone) && !ContentIdRules.IsValidFormat(id.Value)) throw Invalid(reader, "invalid content ID");
            return id;
        }
        void ReadBaseline(SaveReader reader, out string[] names, out int[] slots, out int[] values)
        {
            int count = Count(reader, 5); names = new string[count]; slots = new int[count]; values = new int[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = Name(reader);
                if (i != 0 && string.CompareOrdinal(names[i - 1], names[i]) >= 0) throw Invalid(reader, "baseline names not strictly ordered");
                slots[i] = facts.Catalog.SlotOf(names[i]); values[i] = reader.ReadInt32();
            }
        }
        static List<ContentId> ReadIds(SaveReader reader)
        {
            int count = Count(reader, 1); var ids = new List<ContentId>(count);
            for (int i = 0; i < count; i++)
            {
                var id = Id(reader);
                if (i > 0 && string.CompareOrdinal(ids[i - 1].Value, id.Value) >= 0) throw Invalid(reader, "IDs not strictly ordered");
                ids.Add(id);
            }
            return ids;
        }
        public void Restore(SaveReader reader, int fromVersion)
        {
            if (fromVersion == 0)
            {
                quests.RestoreRecords(Array.Empty<QuestService.SavedRecord>(), default);
                beats.QueueState.Clear(); beats.PerBody.Clear(); beats.ResetForRestore();
                clues.RestoreState(Array.Empty<ContentId>(), Array.Empty<ContentId>()); return;
            }
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (fromVersion != Version) throw Invalid(reader, "unsupported version " + fromVersion);
            int count = Count(reader, 15); var loaded = new List<QuestService.SavedRecord>(count);
            for (int i = 0; i < count; i++)
            {
                var id = Id(reader); var value = new QuestInstance { Instance = Number(reader), Status = (QuestStatus)reader.ReadByte(),
                    Stage = reader.ReadVarInt32(), DoneBits = reader.ReadUInt64(), AcceptOrder = Number(reader) };
                if (value.Status > QuestStatus.Abandoned || value.Stage < -1) throw Invalid(reader, "invalid quest status/stage");
                if (i > 0)
                {
                    var previous = loaded[i - 1]; int order = string.CompareOrdinal(previous.Id.Value, id.Value);
                    if (order > 0 || order == 0 && previous.Value.Instance >= value.Instance) throw Invalid(reader, "quest instances not strictly ordered");
                }
                ReadBaseline(reader, out value.AcceptBaselineNames, out value.AcceptBaselineSlots, out value.AcceptBaselineValues);
                ReadBaseline(reader, out value.StageBaselineNames, out value.StageBaselineSlots, out value.StageBaselineValues);
                int logs = Count(reader, 1);
                for (int j = 0; j < logs; j++)
                {
                    string entry = reader.ReadString();
                    if (string.IsNullOrEmpty(entry)) throw Invalid(reader, "empty log entry ID");
                    value.Log.Add(entry); // Local log IDs need no content prefix/underscore.
                }
                loaded.Add(new QuestService.SavedRecord(id, value));
            }
            var tracked = Id(reader, true);
            count = Count(reader, 13); var perBody = new List<(ContentId Beat, uint System, ulong Key)>(count);
            for (int i = 0; i < count; i++)
            {
                var entry = (Beat: Id(reader), System: reader.ReadUInt32(), Key: reader.ReadUInt64());
                if (i > 0)
                {
                    var previous = perBody[i - 1]; int order = string.CompareOrdinal(previous.Beat.Value, entry.Beat.Value);
                    if (order == 0) order = previous.System.CompareTo(entry.System);
                    if (order == 0) order = previous.Key.CompareTo(entry.Key);
                    if (order >= 0) throw Invalid(reader, "per-body records not strictly ordered");
                }
                perBody.Add(entry);
            }
            count = Count(reader, 19); if (count > 32) throw Invalid(reader, "beat queue exceeds32");
            var queue = new List<BeatQueue.Entry>(count);
            for (int i = 0; i < count; i++)
            {
                var id = Id(reader); bool hasBody = reader.ReadBool();
                var body = new LogicBodyHandle(reader.ReadUInt32(), reader.ReadByte(), reader.ReadUInt16(), reader.ReadUInt16(), new ContentHash(reader.ReadUInt64()));
                int beat = 0; var definitions = catalog.All<StoryBeatDefinition>();
                for (int j = 0; j < definitions.Count; j++) if (definitions[j].Id == id) { beat = j + 1; break; }
                queue.Add(new BeatQueue.Entry(beat, new LogicScope(LogicUsage.BeatEffect, 0, 0, beat, hasBody, body),
                    beat != 0 && definitions[beat - 1].Trigger == BeatTrigger.Condition, id));
            }
            var loadedSeen = ReadIds(reader); var loadedResolved = ReadIds(reader);
            if (!reader.IsAtEnd) throw Invalid(reader, "trailing data");
            // Every parse, ordering and size check completed before the first owner mutation.
            quests.RestoreRecords(loaded, tracked); clues.RestoreState(loadedSeen, loadedResolved);
            beats.QueueState.Clear(); foreach (var entry in queue) beats.QueueState.Add(entry);
            beats.PerBody.Clear(); foreach (var entry in perBody) beats.PerBody.Add(entry);
            beats.ResetForRestore();
        }
    }
}
