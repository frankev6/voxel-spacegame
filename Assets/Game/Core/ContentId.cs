namespace Game.Core
{
    /// <summary>Stable managed content identity (architecture §6.1). Never inside IComponentData, buffers, blobs, jobs or ECS events.
    /// Implements SPEC-02 §4.1.</summary>
    public readonly struct ContentId : System.IEquatable<ContentId>
    {
        public readonly string Value;                      // e.g. "block_hull"; null = None. Format rules: SPEC-04 ContentIdRules
        public readonly ContentHash Hash;                  // StableHash.Hash64(Value); None when Value is null

        public ContentId(string value)
        {
            if (string.IsNullOrEmpty(value)) { Value = null; Hash = default; }
            else { Value = value; Hash = new ContentHash(StableHash.Hash64(value)); }
        }
        public static ContentId None => default;
        public bool IsNone => Value == null;
        public bool Equals(ContentId other) => Hash.Value == other.Hash.Value && string.Equals(Value, other.Value, System.StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ContentId o && Equals(o);
        public override int GetHashCode() => Hash.GetHashCode();
        public override string ToString() => Value ?? "<none>";
        public static bool operator ==(ContentId a, ContentId b) => a.Equals(b);
        public static bool operator !=(ContentId a, ContentId b) => !a.Equals(b);
    }
}
