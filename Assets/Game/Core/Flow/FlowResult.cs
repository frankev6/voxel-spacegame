namespace Game.Core.Flow
{
    /// <summary>Result of an IGameFlow operation (SPEC-02 §4.8).</summary>
    public enum FlowResult : byte { Ok = 0, Busy = 1, Blocked = 2, Failed = 3, NotAllowed = 4 }
}
