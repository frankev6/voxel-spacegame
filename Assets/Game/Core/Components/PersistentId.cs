namespace Game.Core.Components
{
    /// <summary>Stable identity of a runtime-spawned object across save/load (architecture §6.1). Allocated by GameSession (SPEC-01).
    /// Implements SPEC-02 §4.1.</summary>
    public struct PersistentId : Unity.Entities.IComponentData, System.IEquatable<PersistentId>
    {
        public ulong Value;                                // 0 = none; allocated values start at 1 and are never reused
        public PersistentId(ulong value) { Value = value; }
        public bool IsNone => Value == 0UL;
        public bool Equals(PersistentId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PersistentId o && o.Value == Value;
        public override int GetHashCode() => (int)Value ^ (int)(Value >> 32);
        public override string ToString() => "pid:" + Value.ToString();
    }
}
