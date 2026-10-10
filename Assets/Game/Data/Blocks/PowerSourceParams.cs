namespace Game.Data.Blocks
{
    /// <summary>Power source row. Unmanaged; Burst reads it. Implements SPEC-04 §4.6.</summary>
    [System.Serializable] public struct PowerSourceParams { public float Generation; }                  // E/s, > 0
}
