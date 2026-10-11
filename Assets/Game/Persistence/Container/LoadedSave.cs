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
    }
}
