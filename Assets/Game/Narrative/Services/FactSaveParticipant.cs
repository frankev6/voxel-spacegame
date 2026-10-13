using System;
using Game.Data.Validation;
using Game.Core.Save;

namespace Game.Narrative
{
    /// <summary>SPEC-40 §4.11: versioned FACT payload; fully validate before changing the live store.</summary>
    public sealed class FactSaveParticipant : ISaveParticipant
    {
        readonly FactStore store;
        public FactSaveParticipant(FactStore store) => this.store = store ?? throw new ArgumentNullException(nameof(store));
        public string Key => "FACT";
        public int Version => 1;
        public void Capture(SaveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            var snapshot = store.Capture(); writer.WriteInt32(snapshot.Entries.Count);
            foreach (var entry in snapshot.Entries) { writer.WriteString(entry.Name); writer.WriteInt32(entry.Value); }
        }
        public void Restore(SaveReader reader, int fromVersion)
        {
            if (fromVersion == 0) { store.ResetToDefaults(); return; }
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (fromVersion != Version) throw new SaveFormatException("Unsupported FACT version " + fromVersion, reader.Position);
            int count = reader.ReadInt32();
            if (count < 0 || count > reader.Remaining / 5) throw new SaveFormatException("Invalid FACT entry count", reader.Position);
            var snapshot = new FactSnapshot(); string previous = null;
            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                if (!ContentIdRules.IsValidFormat(name)) throw new SaveFormatException("Invalid FACT name", reader.Position);
                if (previous != null && StringComparer.Ordinal.Compare(previous, name) >= 0)
                    throw new SaveFormatException("FACT names must be unique and ordinally sorted", reader.Position);
                snapshot.Entries.Add(new FactEntry(name, reader.ReadInt32())); previous = name;
            }
            if (!reader.IsAtEnd) throw new SaveFormatException("Trailing FACT data", reader.Position);
            store.Restore(snapshot);
        }
    }
}
