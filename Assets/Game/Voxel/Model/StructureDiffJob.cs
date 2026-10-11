using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Model
{
    /// <summary>Implements SPEC-10 §5.4: canonical palette-cell and instance multiset differences; HP/flags are ignored.</summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct StructureDiffJob : IJob
    {
        [ReadOnly] public VoxelStructureData From;
        [ReadOnly] public VoxelStructureData To;
        public NativeList<BlockPlacement> Removed;
        public NativeList<BlockPlacement> Added;

        public unsafe void Execute()
        {
            bool samePalette = From.Palette.Length == To.Palette.Length;
            for (int i = 0; i < From.Palette.Length && samePalette; i++) samePalette = Equal(From.Palette[i], To.Palette[i]);
            using var coords = new NativeList<int3>(From.Bricks.Length + To.Bricks.Length, Allocator.Temp);
            for (int i = 0; i < From.Bricks.Length; i++) coords.Add(From.Bricks[i].Coord);
            for (int i = 0; i < To.Bricks.Length; i++) coords.Add(To.Bricks[i].Coord);
            coords.Sort(new CoordComparer());
            for (int k = 0; k < coords.Length; k++)
            {
                int3 coord = coords[k];
                if (k != 0 && coord.Equals(coords[k - 1])) continue;
                bool hasA = From.BrickIndex.TryGetValue(coord, out int ai);
                bool hasB = To.BrickIndex.TryGetValue(coord, out int bi);
                var a = hasA ? From.Bricks[ai] : default;
                var b = hasB ? To.Bricks[bi] : default;
                if (hasA && hasB && samePalette)
                {
                    if (a.Page < 0 && b.Page < 0 && a.Uniform.Equals(b.Uniform)) continue;
                    if (a.Page >= 0 && b.Page >= 0 && UnsafeUtility.MemCmp(
                        (Cell*)From.CellPages.GetUnsafeReadOnlyPtr() + a.Page * BrickMath.CellsPerBrick,
                        (Cell*)To.CellPages.GetUnsafeReadOnlyPtr() + b.Page * BrickMath.CellsPerBrick,
                        BrickMath.CellsPerBrick * UnsafeUtility.SizeOf<Cell>()) == 0) continue;
                }
                for (int i = 0; i < BrickMath.CellsPerBrick; i++)
                {
                    Cell ac = !hasA ? default : a.Page < 0 ? a.Uniform : From.CellPages[a.Page * BrickMath.CellsPerBrick + i];
                    Cell bc = !hasB ? default : b.Page < 0 ? b.Uniform : To.CellPages[b.Page * BrickMath.CellsPerBrick + i];
                    bool ap = !ac.IsAir && !ac.IsInstance, bp = !bc.IsAir && !bc.IsInstance;
                    PaletteEntry ae = ap ? From.Palette[ac.PaletteIndex] : default;
                    PaletteEntry be = bp ? To.Palette[bc.PaletteIndex] : default;
                    if (ap == bp && (!ap || Equal(ae, be))) continue;
                    int3 cell = BrickMath.CellOf(coord, i);
                    if (ap) Removed.Add(Placement(ae, cell));
                    if (bp) Added.Add(Placement(be, cell));
                }
            }
            using var fromInstances = new NativeList<BlockPlacement>(From.Instances.Length, Allocator.Temp);
            using var toInstances = new NativeList<BlockPlacement>(To.Instances.Length, Allocator.Temp);
            Instances(From.Instances, fromInstances); Instances(To.Instances, toInstances);
            var comparer = new PlacementComparer();
            fromInstances.Sort(comparer); toInstances.Sort(comparer);
            int x = 0, y = 0;
            while (x < fromInstances.Length || y < toInstances.Length)
            {
                int comparison = x == fromInstances.Length ? 1 : y == toInstances.Length ? -1 : comparer.Compare(fromInstances[x], toInstances[y]);
                if (comparison < 0) Removed.Add(fromInstances[x++]);
                else if (comparison > 0) Added.Add(toInstances[y++]);
                else { x++; y++; }
            }
            Removed.Sort(comparer); Added.Sort(comparer);
        }
        static bool Equal(PaletteEntry a, PaletteEntry b) => a.Block == b.Block && a.Orient == b.Orient && a.Color == b.Color;
        static BlockPlacement Placement(PaletteEntry entry, int3 cell) => new BlockPlacement { Block = entry.Block, Origin = cell, Orient = entry.Orient, Color = entry.Color };
        static void Instances(NativeList<BlockInstance> source, NativeList<BlockPlacement> output)
        {
            for (int i = 0; i < source.Length; i++)
            {
                var value = source[i];
                if (value.Block != 0) output.Add(new BlockPlacement { Block = value.Block, Origin = value.Origin, Orient = value.Orient, Color = value.Color });
            }
        }
        struct CoordComparer : IComparer<int3>
        {
            public int Compare(int3 a, int3 b)
            {
                int z = a.z.CompareTo(b.z); if (z != 0) return z;
                int y = a.y.CompareTo(b.y); return y != 0 ? y : a.x.CompareTo(b.x);
            }
        }
        struct PlacementComparer : IComparer<BlockPlacement>
        {
            public int Compare(BlockPlacement a, BlockPlacement b)
            {
                int coord = new CoordComparer().Compare(a.Origin, b.Origin); if (coord != 0) return coord;
                int block = a.Block.CompareTo(b.Block); if (block != 0) return block;
                int orient = a.Orient.CompareTo(b.Orient); return orient != 0 ? orient : a.Color.CompareTo(b.Color);
            }
        }
    }
}
