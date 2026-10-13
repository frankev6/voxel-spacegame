namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.4); later task implementations are explicitly labelled.</summary>
    public interface IRefResolver
    {
        RefKind Kind { get; }
        bool TryResolve(string id, out int index);                      // O(1)
        System.Collections.Generic.IReadOnlyList<string> AllIds { get; } // ordinal order; used for generated facts and suggestions
    }
}
