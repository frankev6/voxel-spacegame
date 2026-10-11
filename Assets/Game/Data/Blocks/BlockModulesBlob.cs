using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Data.Blocks
{
    /// <summary>Every module row of every block. Tables indexed by (int)BlockModuleId. Methods are called through `ref` to the blob value.
    /// Implements SPEC-04 §4.7. Burst-compatible.</summary>
    public struct BlockModulesBlob
    {
        public BlobArray<BlockModuleTable> Tables;   // length = highest BlockModuleId value + 1
        public BlobArray<byte> RowData;

        public bool Has(BlockModuleId id, ushort block)
            => (int)id < Tables.Length && block < Tables[(int)id].RowByBlock.Length && Tables[(int)id].RowByBlock[block] >= 0;

        /// <summary>Copies the row; false (row = default) if absent. Debug builds assert UnsafeUtility.SizeOf&lt;T&gt;() == RowSize.</summary>
        public unsafe bool TryGet<T>(BlockModuleId id, ushort block, out T row) where T : unmanaged
        {
            row = default;
            if (!Has(id, block)) return false;
            ref var table = ref Tables[(int)id];
            UnityEngine.Debug.Assert(UnsafeUtility.SizeOf<T>() == table.RowSize, "BlockModulesBlob.TryGet: row type size differs from the table's RowSize");
            byte* p = (byte*)RowData.GetUnsafePtr() + table.DataOffset + table.RowByBlock[block] * table.RowSize;
            row = UnsafeUtility.ReadArrayElement<T>(p, 0);
            return true;
        }
    }
}
