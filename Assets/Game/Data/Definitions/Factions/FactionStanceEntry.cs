namespace Game.Data.Definitions
{
    /// <summary>SPEC-33 §4.1 authored non-player faction stance.</summary>
    [System.Serializable]
    public struct FactionStanceEntry { public FactionDefinition other; public FactionStanceValue stance; }
}
