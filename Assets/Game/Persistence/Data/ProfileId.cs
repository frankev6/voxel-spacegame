using System;

namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5. Profile folder name, e.g. "p001".</summary>
    public readonly struct ProfileId : IEquatable<ProfileId>
    {
        public readonly string Value;
        public ProfileId(string value) { Value = value; }
        public bool Equals(ProfileId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ProfileId o && Equals(o);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);   // in-memory only
        public override string ToString() => Value ?? "";
    }
}
