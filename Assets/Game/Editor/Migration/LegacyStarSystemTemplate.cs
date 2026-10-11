using System.Collections.Generic;

namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.7.</summary>
    public sealed class LegacyStarSystemTemplate
    {
        public string LegacySystemId; public Game.Editor.Migration.Dto.LegacySolarSystemDataDto Dto;
        public IReadOnlyList<string> StarBodyIds;
        public IReadOnlyList<(string BodyId, float Weight, int MaxCount)> EntityPool;
        public IReadOnlyList<string> ConnectedSystemIds;   // legacy systemIds
    }
}
