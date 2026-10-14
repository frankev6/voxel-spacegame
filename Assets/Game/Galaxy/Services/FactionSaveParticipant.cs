using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;

namespace Game.Galaxy.Services
{
    /// <summary>FACN v1: sorted relations and lossless unresolved identities (SPEC-33 §5.3).</summary>
    public sealed class FactionSaveParticipant : ISaveParticipant
    {
        readonly FactionService factions;
        readonly ContentCatalog catalog;
        public FactionSaveParticipant(FactionService factions, ContentCatalog catalog)
        { this.factions = factions ?? throw new ArgumentNullException(nameof(factions)); this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }
        public string Key => "FACN";
        public int Version => 1;
        public void Capture(SaveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            var rows = new SortedDictionary<string, int>(factions.Unresolved, StringComparer.Ordinal);
            for (byte i = 1; i < factions.Indices.Count; i++)
            { var def = factions.Indices.ByIndex(i); rows[def.IdString] = factions.GetRelation(def); }
            writer.WriteVarUInt32((uint)rows.Count);
            foreach (var row in rows) { writer.WriteContentId(new ContentId(row.Key)); writer.WriteInt32(row.Value); }
        }
        public void Restore(SaveReader reader, int fromVersion)
        {
            if (fromVersion == 0) { factions.InitializeNewGame(default); return; }
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (fromVersion != Version) throw new SaveFormatException("Unsupported FACN version " + fromVersion, reader.Position);
            uint count = reader.ReadVarUInt32();
            if (count > reader.Remaining / 5) throw new SaveFormatException("Invalid FACN record count", reader.Position);
            var resolved = new Dictionary<byte, int>();
            var unresolved = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (uint i = 0; i < count; i++)
            {
                var id = reader.ReadContentId(); int relation = reader.ReadInt32();
                if (id.IsNone || relation < -100 || relation > 100 || !seen.Add(id.Value))
                    throw new SaveFormatException("Invalid or duplicate FACN relation", reader.Position);
                if (catalog.TryResolve<FactionDefinition>(id, "FACN", out var def))
                {
                    byte index = factions.Indices.IndexOf(def);
                    if (index == 255) unresolved.Add(id.Value, relation);
                    else if (!resolved.TryAdd(index, relation)) throw new SaveFormatException("Duplicate resolved FACN faction", reader.Position);
                }
                else unresolved.Add(id.Value, relation);
            }
            if (!reader.IsAtEnd) throw new SaveFormatException("Trailing FACN data", reader.Position);
            // All parsing/validation precedes any live-state mutation, including preserved missing definitions.
            factions.Replace(resolved, unresolved);
        }
    }
}
