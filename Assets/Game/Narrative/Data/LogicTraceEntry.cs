namespace Game.Narrative
{
    /// <summary>Public narrative contract (SPEC-40 §4.10); behavior follows in the named implementation task.</summary>
    public struct LogicTraceEntry { public int Frame; public double UniversalTime; public LogicTraceKind Kind; public Game.Data.Logic.FactWriteSource Source; public int Subject; public int OldValue; public int NewValue; public ulong ContextId; }
}
