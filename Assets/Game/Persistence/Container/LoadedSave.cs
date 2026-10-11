using System.Collections.Generic;

namespace Game.Persistence.Container
{
    /// <summary>Implements SPEC-05 §4.5. Opaque result of ReadAsync, consumed once by Restore.</summary>
    public sealed class LoadedSave
    {
        public Game.Persistence.Data.SlotRef Slot { get; }
        public ContainerHeader Header { get; }
        public Game.Persistence.Data.SaveMeta Meta { get; }
        public bool FromBackup { get; }
        public IReadOnlyList<RawChunk> Chunks { get; }
        public Game.Data.Validation.ValidationReport Report { get; }   // read-phase entries; Restore appends

        internal SaveIdPalette Palette { get; }
        internal Dictionary<string, byte[]> StagedThumbnails { get; }
        internal bool Restored { get; set; }

        internal LoadedSave(Game.Persistence.Data.SlotRef slot, ContainerHeader header, Game.Persistence.Data.SaveMeta meta, bool fromBackup,
                            IReadOnlyList<RawChunk> chunks, Game.Data.Validation.ValidationReport report, SaveIdPalette palette,
                            Dictionary<string, byte[]> stagedThumbnails)
        {
            Slot = slot; Header = header; Meta = meta; FromBackup = fromBackup; Chunks = chunks; Report = report;
            Palette = palette; StagedThumbnails = stagedThumbnails;
        }
    }
}
