namespace Game.Narrative
{
    /// <summary>Public narrative contract (SPEC-40 §4.10); behavior follows in the named implementation task.</summary>
    public readonly struct FactEntry { public readonly string Name; public readonly int Value; public FactEntry(string name, int value) { Name = name; Value = value; } }
}
