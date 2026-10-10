namespace Game.Data.Blocks
{
    /// <summary>Cargo row: capacity units (SPEC-50: 10 units = 1 slot). Unmanaged; Burst reads it. Implements SPEC-04 §4.6.</summary>
    [System.Serializable] public struct CargoParams { public float Capacity; }                          // > 0
}
