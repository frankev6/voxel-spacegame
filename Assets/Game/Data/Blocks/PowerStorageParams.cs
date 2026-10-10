namespace Game.Data.Blocks
{
    /// <summary>Power storage row. Unmanaged; Burst reads it. Implements SPEC-04 §4.6.</summary>
    [System.Serializable] public struct PowerStorageParams { public float Storage; }                    // E, > 0
}
