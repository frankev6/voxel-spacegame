namespace Game.Data.Blocks
{
    /// <summary>Set of cell faces (SPEC-04 §4.2).</summary>
    [System.Flags] public enum BlockFaceMask : byte
    { None = 0, PosX = 1, NegX = 2, PosY = 4, NegY = 8, PosZ = 16, NegZ = 32, All = 63 }
}
