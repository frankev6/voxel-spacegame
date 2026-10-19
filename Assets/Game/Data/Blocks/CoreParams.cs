namespace Game.Data.Blocks
{
    /// <summary>Core module row: build budget and max AABB edge (SPEC-51 enforces), and the floor below which the core malfunctions
    /// (owner, 2026-10-18: "cores should have a minimum and a maximum threshold of voxels they can hold ... the minimum threshold
    /// of voxels should generally be 20% of the max threshold. if it gets into that critical area it malfunctions and becomes a
    /// wreck"). A ship whose live cells fall below <see cref="Floor"/> becomes a hulk (SPEC-13 §5.6, HulkCause.CoreMalfunction).
    /// Unmanaged; Burst reads it. Implements SPEC-04 §4.6.</summary>
    [System.Serializable] public struct CoreParams
    {
        public int MaxCells; public int MaxExtentCells;   // ≥ 1 each
        public int MinCells;                              // 0 = MaxCells × DefaultFloorFraction; otherwise 1..MaxCells
        public const float DefaultFloorFraction = .2f;
        /// <summary>The fewest live cells the core keeps working with.</summary>
        public readonly int Floor => MinCells > 0 ? MinCells : (int)(MaxCells * DefaultFloorFraction);
        /// <summary>A design of `cells` fits this core: at least its floor and at most its budget.</summary>
        public readonly bool Fits(int cells) => cells >= Floor && cells <= MaxCells;
    }
}
