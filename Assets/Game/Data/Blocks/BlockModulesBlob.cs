namespace Game.Data.Blocks
{
    /// <summary>Every module row of every block. Tables indexed by (int)BlockModuleId. Methods are called through `ref` to the blob value.
    /// Implements SPEC-04 §4.7.</summary>
    public struct BlockModulesBlob
    {
        public Unity.Entities.BlobArray<BlockModuleTable> Tables;   // length = highest BlockModuleId value + 1
        public Unity.Entities.BlobArray<byte> RowData;
        public bool Has(BlockModuleId id, ushort block) { throw new System.NotImplementedException("DATA-006"); }
        /// <summary>Copies the row; false (row = default) if absent. Debug builds assert UnsafeUtility.SizeOf&lt;T&gt;() == RowSize.</summary>
        public bool TryGet<T>(BlockModuleId id, ushort block, out T row) where T : unmanaged { throw new System.NotImplementedException("DATA-006"); }
    }
}
