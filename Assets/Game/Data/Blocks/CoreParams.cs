namespace Game.Data.Blocks
{
    /// <summary>Core module row: build budget and max AABB edge (SPEC-51 enforces). Unmanaged; Burst reads it. Implements SPEC-04 §4.6.</summary>
    [System.Serializable] public struct CoreParams { public int MaxCells; public int MaxExtentCells; }   // ≥ 1 each
}
