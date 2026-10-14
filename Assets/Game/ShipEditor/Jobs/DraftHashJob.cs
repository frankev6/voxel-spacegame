using System.Collections.Generic;
using Game.Core;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.ShipEditor.Jobs
{
    /// <summary>SPEC-51 EDT-003: content hash independent of palette and instance allocation order.</summary>
    [BurstCompile]
    public struct DraftHashJob : IJob
    {
        [ReadOnly] public VoxelStructureReadView Source;
        [ReadOnly] public NativeArray<int> BrickOrder;
        public NativeReference<ulong> Result;
        public void Execute()
        {
            ulong hash = 0;
            for (int k = 0; k < BrickOrder.Length; k++)
            {
                int brick = BrickOrder[k];
                for (int local = 0; local < BrickMath.CellsPerBrick; local++)
                {
                    var cell = Source.Read(brick, local); if (cell.IsAir) continue;
                    ushort block; byte orient, color;
                    if (cell.IsInstance)
                    { var value = Source.Instances[cell.InstanceIndex]; block = value.Block; orient = value.Orient; color = value.Color; }
                    else { var value = Source.Palette[cell.PaletteIndex]; block = value.Block; orient = value.Orient; color = value.Color; }
                    hash = Coordinate(hash, BrickMath.CellOf(Source.Bricks[brick].Coord, local));
                    hash = StableHash.Combine(hash, block); hash = StableHash.Combine(hash, orient);
                    hash = StableHash.Combine(hash, color); hash = StableHash.Combine(hash, cell.Hp);
                }
            }
            using var instances = new NativeList<BlockInstance>(Allocator.Temp);
            for (int i = 0; i < Source.Instances.Length; i++)
            { var instance = Source.Instances[i]; if (instance.Block != 0 && instance.CellsAlive != 0) instances.Add(instance); }
            instances.AsArray().Sort(new OriginOrder());
            for (int i = 0; i < instances.Length; i++)
            { hash = Coordinate(hash, instances[i].Origin); hash = StableHash.Combine(hash, math.asuint(instances[i].FunctionHp)); }
            Result.Value = hash;
        }
        static ulong Coordinate(ulong hash, int3 cell)
        {
            hash = StableHash.Combine(hash, unchecked((uint)cell.x));
            hash = StableHash.Combine(hash, unchecked((uint)cell.y));
            return StableHash.Combine(hash, unchecked((uint)cell.z));
        }
        struct OriginOrder : IComparer<BlockInstance>
        {
            public int Compare(BlockInstance a, BlockInstance b)
            { int c = a.Origin.z.CompareTo(b.Origin.z); if (c != 0) return c; c = a.Origin.y.CompareTo(b.Origin.y); return c != 0 ? c : a.Origin.x.CompareTo(b.Origin.x); }
        }
    }
}
