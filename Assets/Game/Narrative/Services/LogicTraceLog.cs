namespace Game.Narrative
{
    /// <summary>Public narrative contract (SPEC-40 §4.10); behavior follows in the named implementation task.</summary>
    public sealed class LogicTraceLog
    {
        // TODO(NAR-007): implement trace storage only in UNITY_EDITOR || DEVELOPMENT_BUILD; keep FactStore.Trace null in release.
        public const int Capacity = 512;
        public void Add(in LogicTraceEntry e) => throw new System.NotImplementedException("NAR-007");
        public int CopyTo(LogicTraceEntry[] buffer) => throw new System.NotImplementedException("NAR-007");
        public int Version => throw new System.NotImplementedException("NAR-007");
    }
}
