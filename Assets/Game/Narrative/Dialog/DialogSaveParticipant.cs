using System;
using System.Collections.Generic;
using Game.Core.Save;

namespace Game.Narrative.Dialog
{
    /// <summary>SPEC-41 §4.10: canonical DLGS memory payload. Registration belongs to DLG-007.</summary>
    public sealed class DialogSaveParticipant : ISaveParticipant
    {
        readonly DialogMemory memory;
        public DialogSaveParticipant(DialogMemory memory) => this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
        public string Key => "DLGS";
        public int Version => 1;

        public void Capture(SaveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            var entries = new List<(string key, int value)>();
            memory.CopySorted(entries);
            writer.WriteInt32(entries.Count);
            foreach (var entry in entries)
            {
                writer.WriteString(entry.key);
                writer.WriteInt32(entry.value);
            }
        }

        public void Restore(SaveReader reader, int fromVersion)
        {
            if (fromVersion == 0) { memory.Clear(); return; }
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (fromVersion != Version)
                throw new SaveFormatException("Unsupported DLGS version " + fromVersion, reader.Position);
            int count = reader.ReadInt32();
            if (count < 0 || count > reader.Remaining / 5)
                throw new SaveFormatException("Invalid DLGS entry count", reader.Position);
            var entries = new List<(string key, int value)>();
            string previous = null;
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                if (string.IsNullOrEmpty(key) || (previous != null && string.CompareOrdinal(previous, key) >= 0))
                    throw new SaveFormatException("DLGS keys must be nonempty, unique and ordinally sorted", reader.Position);
                entries.Add((key, reader.ReadInt32()));
                previous = key;
            }
            if (!reader.IsAtEnd) throw new SaveFormatException("Trailing DLGS data", reader.Position);
            // Validate the entire payload before replacing the live memory.
            memory.Clear();
            foreach (var entry in entries) memory.SetRaw(entry.key, entry.value);
        }
    }
}
