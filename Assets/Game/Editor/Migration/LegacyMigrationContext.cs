namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. Everything a converter needs; built by LegacyMigrationRunner.</summary>
    public sealed class LegacyMigrationContext
    {
        public LegacyContentIndex Index { get; }
        public LegacyIdMap IdMap { get; }
        public LegacyAssetWriter Writer { get; }
        public Game.Data.Validation.ValidationReport Report { get; }
        public Game.Data.Services.ContentCatalog Catalog { get; }      // rebuilt by the runner after every converter
        public LegacyHandOff HandOff { get; }
        public bool Force { get; }                                     // overwrite hand-edited assets
        public string LegacyRoot { get; }                              // absolute path of Import/Legacy
    }
}
