using System;
using Game.Core.Save;
using Unity.Collections;

namespace Game.Voxel.Data
{
    /// <summary>SPEC-05 §5.7: main-thread native snapshot, worker encoding, disposal on either thread.</summary>
    public sealed class VoxsSnapshotBlock : ISaveDeferredBlock
    {
        VoxsSource copy;
        readonly VoxsBlockNames names;
        bool disposed;

        VoxsSnapshotBlock(VoxsBlockNames names) { this.names = names; }

        public static VoxsSnapshotBlock Capture(in VoxsSource source, VoxsBlockNames names)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            var block = new VoxsSnapshotBlock(names);
            try
            {
                block.copy.CellSize = source.CellSize;
                block.copy.AnchorInstance = source.AnchorInstance;
                block.copy.Partial = source.Partial;
                block.copy.Palette = Copy(source.Palette);
                block.copy.Instances = Copy(source.Instances);
                block.copy.BrickCoords = Copy(source.BrickCoords);
                block.copy.Cells = Copy(source.Cells);
                block.copy.RemovedBricks = Copy(source.RemovedBricks);
                return block;
            }
            catch { block.Dispose(); throw; }
        }

        static NativeArray<T> Copy<T>(NativeArray<T> source) where T : struct
            => source.IsCreated ? new NativeArray<T>(source, Allocator.Persistent) : default;

        public byte[] Encode()
        {
            if (disposed) throw new ObjectDisposedException(nameof(VoxsSnapshotBlock));
            return VoxsCodec.Encode(copy, names, VoxsEncodeOptions.ForSaveChunk);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (copy.Palette.IsCreated) copy.Palette.Dispose();
            if (copy.Instances.IsCreated) copy.Instances.Dispose();
            if (copy.BrickCoords.IsCreated) copy.BrickCoords.Dispose();
            if (copy.Cells.IsCreated) copy.Cells.Dispose();
            if (copy.RemovedBricks.IsCreated) copy.RemovedBricks.Dispose();
        }
    }
}
