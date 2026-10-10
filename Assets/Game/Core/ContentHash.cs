namespace Game.Core
{
    /// <summary>Unmanaged content reference for ECS/Burst data. Implements SPEC-02 §4.1.</summary>
    public readonly struct ContentHash : System.IEquatable<ContentHash>
    {
        public readonly ulong Value;                       // StableHash.Hash64(id string); 0 = none
        public ContentHash(ulong value) { Value = value; }
        public static ContentHash None => default;
        public bool IsNone => Value == 0UL;
        public bool Equals(ContentHash other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ContentHash o && o.Value == Value;
        public override int GetHashCode() => (int)Value ^ (int)(Value >> 32);   // in-memory only, never persisted
        public override string ToString() => "0x" + Value.ToString("X16");
        public static bool operator ==(ContentHash a, ContentHash b) => a.Value == b.Value;
        public static bool operator !=(ContentHash a, ContentHash b) => a.Value != b.Value;
    }
}
