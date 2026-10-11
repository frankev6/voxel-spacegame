namespace Game.Data.Validation
{
    /// <summary>What a definition's Validate can know about the pass it runs in (SPEC-04 §4.8).</summary>
    public sealed class ContentValidationContext
    {
        readonly System.Func<Game.Data.Definitions.ContentDefinition, bool> inDatabase;

        /// <summary>Built by ContentService (runtime pass) and ContentValidatorRunner (editor pass, DATA-008).</summary>
        internal ContentValidationContext(string assetPath, bool isEditorPass, System.Func<Game.Data.Definitions.ContentDefinition, bool> inDatabase)
        {
            AssetPath = assetPath ?? string.Empty;
            IsEditorPass = isEditorPass;
            this.inDatabase = inDatabase;
        }

        public string AssetPath { get; }                         // editor: AssetDatabase path; runtime: "content:<id>"
        public bool IsInDatabase(Game.Data.Definitions.ContentDefinition def) => def != null && inDatabase != null && inDatabase(def);
        public bool IsEditorPass { get; }                        // true when run by the editor validator (asset paths available)
    }
}
