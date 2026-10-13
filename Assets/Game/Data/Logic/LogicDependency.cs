namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public readonly struct LogicDependency : System.IEquatable<LogicDependency>
    {
        public readonly LogicDependencyKind Kind; public readonly int Key;
        public LogicDependency(LogicDependencyKind kind, int key) { Kind = kind; Key = key; }
        public bool Equals(LogicDependency o) => Kind == o.Kind && Key == o.Key;
        public override bool Equals(object obj) => obj is LogicDependency other && Equals(other);
        public override int GetHashCode() => unchecked(((int)Kind * 397) ^ Key);
    }
}
