namespace Game.Core
{
    /// <summary>Marks a static field that StaticStateAuditTests must accept (immutable after first use, or reset elsewhere). Reason required.
    /// Implements SPEC-01 §4.1.</summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class StaticStateOkAttribute : System.Attribute { public readonly string Reason; public StaticStateOkAttribute(string reason) { Reason = reason; } }
}
