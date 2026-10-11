namespace Game.Input
{
    /// <summary>Outcome of an interactive rebind. Implements SPEC-03 §4.3.</summary>
    public enum RebindStatus : byte { Completed = 0, Cancelled = 1, Conflict = 2, Failed = 3 }
}
