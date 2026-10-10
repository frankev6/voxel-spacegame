namespace Game.Core
{
    /// <summary>Handle for one pause request. Dispose releases it; disposing twice is a no-op. Implements SPEC-02 §4.4.</summary>
    public readonly struct PauseToken : System.IDisposable, System.IEquatable<PauseToken>
    {
        public readonly uint Id;                              // 0 = default/invalid
        private readonly IPauseService owner;
        public PauseToken(IPauseService owner, uint id) { this.owner = owner; Id = id; }
        public bool IsValid => owner != null && Id != 0;
        public void Dispose() { if (owner != null && Id != 0) owner.Release(this); }
        public bool Equals(PauseToken other) => Id == other.Id && ReferenceEquals(owner, other.owner);
        public override bool Equals(object obj) => obj is PauseToken o && Equals(o);
        public override int GetHashCode() => (int)Id;
    }
}
