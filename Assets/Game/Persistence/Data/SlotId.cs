using System;

namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5. Slot folder name, e.g. "manual_001", "quick", "auto_0".</summary>
    public readonly struct SlotId : IEquatable<SlotId>
    {
        public readonly string Value;
        public SlotId(string value) { Value = value; }
        public bool Equals(SlotId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SlotId o && Equals(o);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);   // in-memory only
        public override string ToString() => Value ?? "";
    }
}
