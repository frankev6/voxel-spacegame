using System;
using Game.Data.Blocks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §4.3: session-owned rotated footprints, preserving canonical cell order.</summary>
    public sealed class FootprintCache : IDisposable
    {
        public sealed class Footprint
        {
            internal NativeArray<int3> cells, clearance;
            public NativeArray<int3>.ReadOnly Cells => cells.AsReadOnly();
            public NativeArray<int3>.ReadOnly Clearance => clearance.AsReadOnly();
            public int3 Min { get; internal set; }
            public int3 Max { get; internal set; }
            public BlockFaceMask AttachFaces { get; internal set; }
            internal void Dispose()
            { if (cells.IsCreated) cells.Dispose(); if (clearance.IsCreated) clearance.Dispose(); }
        }
        readonly Footprint[] entries;
        bool disposed;
        public int BlockCount { get; }
        public FootprintCache(BlobAssetReference<BlockDefsBlob> definitions, BlobAssetReference<OrientationTablesBlob> orientations)
        {
            if (!definitions.IsCreated) throw new ArgumentException("Block definitions are required.", nameof(definitions));
            if (!orientations.IsCreated) throw new ArgumentException("Orientation tables are required.", nameof(orientations));
            ref var defs = ref definitions.Value; ref var rotations = ref orientations.Value;
            BlockCount = defs.Blocks.Length; entries = new Footprint[checked(BlockCount * 24)];
            try
            {
                for (int block = 0; block < BlockCount; block++)
                {
                    ref var def = ref defs.Blocks[block];
                    for (byte orient = 0; orient < 24; orient++)
                    {
                        var entry = new Footprint(); entries[block * 24 + orient] = entry;
                        entry.cells = new NativeArray<int3>(def.Cells.Length, Allocator.Persistent);
                        entry.clearance = new NativeArray<int3>(def.Clearance.Length, Allocator.Persistent);
                        for (int i = 0; i < def.Cells.Length; i++)
                        {
                            int3 cell = rotations.Rotate(def.Cells[i], orient); entry.cells[i] = cell;
                            entry.Min = i == 0 ? cell : math.min(entry.Min, cell);
                            entry.Max = i == 0 ? cell : math.max(entry.Max, cell);
                        }
                        for (int i = 0; i < def.Clearance.Length; i++) entry.clearance[i] = rotations.Rotate(def.Clearance[i], orient);
                        byte mask = 0;
                        for (int face = 0; face < 6; face++)
                            if (((byte)def.AttachFaces & (1 << face)) != 0)
                                mask |= (byte)(1 << (int)rotations.RotateFace((BlockFace)face, orient));
                        entry.AttachFaces = (BlockFaceMask)mask;
                    }
                }
            }
            catch { Dispose(); throw; }
        }
        public Footprint Get(int block, byte orient)
        {
            if (disposed) throw new ObjectDisposedException(nameof(FootprintCache));
            if ((uint)block >= (uint)BlockCount) throw new ArgumentOutOfRangeException(nameof(block));
            if (orient >= 24) throw new ArgumentOutOfRangeException(nameof(orient));
            return entries[block * 24 + orient];
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var entry in entries) entry?.Dispose();
        }
    }
}
