using System;
using System.Collections.Generic;

namespace Game.Persistence.Container
{
    /// <summary>Implements SPEC-05 §4.5.</summary>
    public static class SaveContainerReader
    {
        public static ContainerReadStatus Read(ReadOnlySpan<byte> file, out ContainerHeader header,
            List<RawChunk> chunks, Game.Data.Validation.ValidationReport report, string sourceLabel) { throw new System.NotImplementedException("PERS-003"); }
    }
}
