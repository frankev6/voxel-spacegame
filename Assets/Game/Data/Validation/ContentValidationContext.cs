namespace Game.Data.Validation
{
    /// <summary>What a definition's Validate can know about the pass it runs in (SPEC-04 §4.8).</summary>
    public sealed class ContentValidationContext
    {
        public string AssetPath { get; }                         // editor: AssetDatabase path; runtime: "content:<id>"
        public bool IsInDatabase(Game.Data.Definitions.ContentDefinition def) { throw new System.NotImplementedException(); }
        public bool IsEditorPass { get; }                        // true when run by the editor validator (asset paths available)
    }
}
