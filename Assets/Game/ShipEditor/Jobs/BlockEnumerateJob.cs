using System.Collections.Generic;
using Game.ShipEditor.Data;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.ShipEditor.Jobs
{
    /// <summary>SPEC-51 §5.1: palette cells once, live instances once even when their root cell is missing.</summary>
    [BurstCompile]
    public struct BlockEnumerateJob : IJob
    {
        [ReadOnly] public VoxelStructureReadView Source;
        [ReadOnly] public NativeArray<int> BrickOrder;
        public NativeList<BlockRecord> Blocks;
        public void Execute()
        {
            Blocks.Clear();
            for (int k = 0; k < BrickOrder.Length; k++)
            {
                int brick = BrickOrder[k];
                for (int local = 0; local < BrickMath.CellsPerBrick; local++)
                {
                    var cell = Source.Read(brick, local); if (cell.IsAir || cell.IsInstance) continue;
                    var palette = Source.Palette[cell.PaletteIndex];
                    Blocks.Add(new BlockRecord { Origin = BrickMath.CellOf(Source.Bricks[brick].Coord, local),
                        Block = palette.Block, Orient = palette.Orient, Color = palette.Color, DamageOffset = -1 });
                }
            }
            for (int i = 0; i < Source.Instances.Length; i++)
            {
                var instance = Source.Instances[i]; if (instance.Block == 0 || instance.CellsAlive == 0) continue;
                Blocks.Add(new BlockRecord { Origin = instance.Origin, Block = instance.Block,
                    Orient = instance.Orient, Color = instance.Color, DamageOffset = -1 });
            }
            Blocks.AsArray().Sort(new OriginOrder());
        }
        struct OriginOrder : IComparer<BlockRecord>
        {
            public int Compare(BlockRecord a, BlockRecord b)
            { int c = a.Origin.z.CompareTo(b.Origin.z); if (c != 0) return c; c = a.Origin.y.CompareTo(b.Origin.y); return c != 0 ? c : a.Origin.x.CompareTo(b.Origin.x); }
        }
    }
}
