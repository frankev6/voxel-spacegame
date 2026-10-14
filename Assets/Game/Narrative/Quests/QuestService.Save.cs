using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Logic;

namespace Game.Narrative.Quests
{
    public sealed partial class QuestService
    {
        internal readonly struct SavedRecord
        {
            internal readonly ContentId Id;
            internal readonly QuestInstance Value;
            internal SavedRecord(ContentId id, QuestInstance value) { Id = id; Value = value; }
        }
        readonly List<SavedRecord> rawSaved = new();
        ContentId unknownTracked;
        internal ContentId SavedTracked => TrackedQuestIndex == 0 ? unknownTracked : definitions[TrackedQuestIndex].Id;
        internal void CaptureRecords(List<SavedRecord> into)
        {
            into.Clear(); into.AddRange(rawSaved);
            for (int q = 1; q < definitions.Length; q++)
                foreach (var instance in instances[q]) into.Add(new SavedRecord(definitions[q].Id, instance));
            into.Sort((a, b) => { int order = string.CompareOrdinal(a.Id.Value, b.Id.Value); return order != 0 ? order : a.Value.Instance.CompareTo(b.Value.Instance); });
        }
        internal void RestoreRecords(IReadOnlyList<SavedRecord> records, ContentId tracked)
        {
            OnNewGame();
            // A changed definition or non-contiguous instance history stays wholly raw, never partially playable.
            for (int start = 0; start < records.Count;)
            {
                int end = start + 1; while (end < records.Count && records[end].Id == records[start].Id) end++;
                int q = 0;
                for (int i = 1; i < definitions.Length; i++) if (definitions[i].Id == records[start].Id) { q = i; break; }
                bool usable = q != 0;
                for (int i = start; usable && i < end; i++)
                {
                    var value = records[i].Value;
                    usable = value.Instance == i - start &&
                        (value.Status != QuestStatus.Active || (uint)value.Stage < (uint)definitions[q].Stages.Count);
                }
                for (int i = start; i < end; i++)
                {
                    var record = records[i]; acceptCounter = Math.Max(acceptCounter, record.Value.AcceptOrder);
                    if (usable) { record.Value.QuestIndex = q; instances[q].Add(record.Value); }
                    else rawSaved.Add(record);
                }
                start = end;
            }
            unknownTracked = tracked;
            for (int q = 1; q < definitions.Length; q++)
                if (definitions[q].Id == tracked && GetStatus(q) == QuestStatus.Active)
                { TrackedQuestIndex = q; unknownTracked = default; break; }
        }
    }
}
