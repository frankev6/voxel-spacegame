namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public readonly struct LogicDependency : System.IEquatable<LogicDependency>
    {
        // TODO(NAR-003): implement dependency equality/hash alongside binding and collection.
        public readonly LogicDependencyKind Kind; public readonly int Key;
        public LogicDependency(LogicDependencyKind kind, int key) { Kind = kind; Key = key; }
        public bool Equals(LogicDependency o) => throw new System.NotImplementedException("NAR-003");
        public override int GetHashCode() => throw new System.NotImplementedException("NAR-003");
    }
}
