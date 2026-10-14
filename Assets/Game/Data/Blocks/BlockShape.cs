namespace Game.Data.Blocks
{
    /// <summary>Cell geometry of a structural block (SPEC-04 §4.2 shape table; ADR-0018 slope kit). LongSlopeHigh and LongSlopeLow are the two
    /// cells of the 1×2 long slope; each cell stores its own shape. Xeno (ADR-0115) is grown alien tissue: the cell keeps a depth value in
    /// its flags and the mesher builds a smooth surface-nets shell instead of a block shape; physics and damage still see a full cell.</summary>
    public enum BlockShape : byte { Cube = 0, Wedge = 1, HipCorner = 2, LongSlopeHigh = 3, LongSlopeLow = 4, Xeno = 5 }   // add values at the end only
}
