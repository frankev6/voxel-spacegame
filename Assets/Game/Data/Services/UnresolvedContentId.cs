namespace Game.Data.Services
{
    /// <summary>A content ID read from a save that did not resolve (SPEC-04 §4.9, Decision 15).</summary>
    public readonly struct UnresolvedContentId { public readonly string Id; public readonly string ExpectedType; public readonly string Context; }
}
