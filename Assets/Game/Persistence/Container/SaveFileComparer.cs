using System;

namespace Game.Persistence.Container
{
    /// <summary>Implements SPEC-05 §4.5. Compares two save files ignoring volatile fields (decision 12). Used by tests and tools.</summary>
    public static class SaveFileComparer
    {
        public static bool EqualIgnoringVolatile(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, out string firstDifference) { throw new System.NotImplementedException("PERS-003"); }
    }
}
