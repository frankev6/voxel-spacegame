namespace Game.Data.Validation
{
    /// <summary>Thrown at boot when content has errors (SPEC-04 §4.8, Decision 14).</summary>
    public sealed class ContentValidationException : System.Exception
    {
        public ContentValidationException(ValidationReport report)
            : base((report ?? throw new System.ArgumentNullException(nameof(report))).Context + ": content validation failed\n" + report.ToText(50))
        { Report = report; }

        public ValidationReport Report { get; }
    }
}
