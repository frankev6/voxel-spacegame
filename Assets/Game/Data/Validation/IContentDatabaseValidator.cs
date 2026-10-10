namespace Game.Data.Validation
{
    /// <summary>Extra database-wide checks contributed by other specs (e.g. SPEC-10 blueprints, SPEC-40 narrative). Implements SPEC-04 §4.8.</summary>
    public interface IContentDatabaseValidator
    {
        int Order { get; }                                       // ascending; ties by type full name
        void Validate(Game.Data.Services.ContentCatalog catalog, ValidationReport report);
    }
}
