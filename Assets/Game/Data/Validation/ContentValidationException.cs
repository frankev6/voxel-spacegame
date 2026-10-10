namespace Game.Data.Validation
{
    /// <summary>Thrown at boot when content has errors (SPEC-04 §4.8, Decision 14).</summary>
    public sealed class ContentValidationException : System.Exception
    { public ContentValidationException(ValidationReport report) { throw new System.NotImplementedException("DATA-002"); } public ValidationReport Report { get; } }
}
