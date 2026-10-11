using Game.Data.Blocks;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §5.1. One work index owns fixed, disjoint snapshot and prop slices.</summary>
    [BurstCompile]
    internal struct BrickSnapshotJob : IJobParallelFor
    {
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public BufferLookup<BrickSlot> Slots;
        [ReadOnly] public BufferLookup<BrickCells> Cells;
        [ReadOnly] public BufferLookup<BrickSparseCells> Sparse;
        [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;
        public NativeArray<BrickMeshWork> Work;
        [NativeDisableParallelForRestriction] public NativeArray<uint> Snapshots;
        [NativeDisableParallelForRestriction] public NativeArray<BrickPropInstance> Props;

        public void Execute(int i)
        {
            var work = Work[i]; var brick = Bricks[work.Brick]; var grid = Grids[work.Grid];
            var slots = Slots[work.Grid]; var palette = Palettes[work.Grid].AsNativeArray(); var instances = Instances[work.Grid].AsNativeArray();
            work.PropStart = i * 512; work.PropCount = 0; work.SnapshotFlags = 0;
            // Each of the 27 regions resolves its neighbour and buffer pair once. These regions partition all 18^3 positions.
            for (int bz = -1; bz <= 1; bz++) for (int by = -1; by <= 1; by++) for (int bx = -1; bx <= 1; bx++)
            {
                int3 offset = new int3(bx,by,bz);
                int slot = BrickMath.SlotIndex(brick.Coord + offset, grid.BrickMin, grid.BrickDims);
                Entity neighbour = slot >= 0 && slot < slots.Length ? slots[slot].Brick : Entity.Null;
                bool exists = neighbour != Entity.Null && Cells.HasBuffer(neighbour) && Sparse.HasBuffer(neighbour);
                var dense = exists ? Cells[neighbour] : default;
                var sparse = exists ? Sparse[neighbour] : default;
                int3 min = math.select(new int3(-1), new int3(0), offset == 0);
                min = math.select(min, new int3(16), offset == 1);
                int3 max = math.select(min, new int3(15), offset == 0);
                for (int z = min.z; z <= max.z; z++) for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
                {
                    int3 local = new int3(x,y,z);
                    Cell cell = exists ? BrickWrite.Get(dense, sparse, BrickMath.LocalIndex(local & 15)) : default;
                    uint encoded = BrickSnapshotUtility.Encode(cell, brick.Coord * 16 + local, palette, instances,
                        ref Blocks.Value, ref Orientations.Value, ref work.SnapshotFlags, out var prop, out bool isPropRoot);
                    Snapshots[i * MeshCell.SnapCells + MeshCell.SnapIndex(x,y,z)] = encoded;
                    if (!isPropRoot || math.any(offset != 0)) continue;
                    if (work.PropCount == 512) { work.SnapshotFlags |= 2; continue; }
                    prop.LocalIndex = (ushort)BrickMath.LocalIndex(local);
                    Props[work.PropStart + work.PropCount++] = prop;
                }
            }
            Work[i] = work;
        }
    }
}
