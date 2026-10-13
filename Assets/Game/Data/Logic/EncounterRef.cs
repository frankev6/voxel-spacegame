namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.4); later task implementations are explicitly labelled.</summary>
    [System.Serializable]
    public struct EncounterRef
    {
    public string Id;
    [System.NonSerialized] public int Index;
    public const RefKind Kind = RefKind.Encounter;
    }
}
