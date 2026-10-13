namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public struct DiffOp
    {
        public BlockRecord Record;
        public DiffOpKind Kind;
        public byte Pad0;
        public ushort Pad1;
    }
}
