namespace Game.Data.Blocks
{
    /// <summary>Rows of one module type for every block (ADR-0013). Implements SPEC-04 §4.7.</summary>
    public struct BlockModuleTable
    {
        public BlockModuleId Id;
        public int RowSize;                                   // bytes; 0 when no block uses this module
        public int DataOffset;                                // into BlockModulesBlob.RowData; 16-byte aligned
        public Unity.Entities.BlobArray<short> RowByBlock;    // length = Blocks.Length; −1 = the block has no such module
    }
}
