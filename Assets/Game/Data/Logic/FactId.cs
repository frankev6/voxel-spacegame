namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.2); later task implementations are explicitly labelled.</summary>
    public readonly struct FactId : System.IEquatable<FactId>
    {
        public readonly Game.Core.ContentId Id;
        public FactId(string name) { Id = new Game.Core.ContentId(name); }   // no format check here; FactCatalog/compiler check
        public string Name => Id.Value;
        public ulong Hash => Id.Hash.Value;                                   // ContentId.Hash is a ContentHash (architecture §6.1)
        public bool IsValid => !string.IsNullOrEmpty(Id.Value);
        public bool Equals(FactId other) => Hash == other.Hash;
        public override bool Equals(object obj) => obj is FactId f && Equals(f);
        public override int GetHashCode() => (int)Hash;                       // in-memory dictionaries only, never persisted
        public override string ToString() => Id.Value;
    }
}
