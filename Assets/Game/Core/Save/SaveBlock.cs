namespace Game.Core.Save
{
    /// <summary>Opaque handle for a length-prefixed block (SPEC-02 §4.7, §5.7).</summary>
    public readonly struct SaveBlock
    {
        internal readonly int Start;                         // writer: position of the 4-byte length; reader: end position
        internal readonly int Depth;
        internal SaveBlock(int start, int depth) { Start = start; Depth = depth; }
    }
}
