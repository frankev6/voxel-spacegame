namespace Game.Data.Blocks
{
    /// <summary>All block definitions indexed by BlockIndex. Blocks[0] is the air entry. Implements SPEC-04 §4.7.</summary>
    public struct BlockDefsBlob
    {
        public Unity.Entities.BlobArray<BlockDef> Blocks;   // length = blockCount + 1
        public ulong BlockDefsHash;                          // database fingerprint of all BlockDefs, §5.5
        public ushort MissingBlockIndex;                     // index of block_missing
    }
}
