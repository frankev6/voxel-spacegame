using System.Collections.Generic;
using Game.Data.Services;
using Game.Data.Validation;

namespace Game.Editor.Migration
{
    /// <summary>SPEC-72 §4.2: one main-thread conversion run and its current catalog.</summary>
    public sealed class LegacyMigrationContext
    {
        public LegacyContentIndex Index { get; }
        public LegacyIdMap IdMap { get; }
        public LegacyAssetWriter Writer { get; }
        public ValidationReport Report { get; }
        public ContentCatalog Catalog { get; internal set; }
        public LegacyHandOff HandOff { get; internal set; }
        public bool Force { get; }
        public string LegacyRoot { get; }
        internal List<MigrationShipRow> Ships { get; } = new();
        internal LegacyMigrationContext(LegacyContentIndex index, LegacyIdMap map, LegacyAssetWriter writer, ValidationReport report, bool force, string legacyRoot)
        {
            Index = index; IdMap = map; Writer = writer; Report = report; Force = force; LegacyRoot = legacyRoot;
        }
    }
}
