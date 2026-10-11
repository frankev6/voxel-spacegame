using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Services;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>SPEC-05 PERS-007: owned native inputs, independent of VoxelStructure implementation.</summary>
    internal sealed class VoxsTestStructures : IDisposable
    {
        public VoxsSource Source;
        public VoxsTestStructures(int bricks, int palette = 2, int instances = 0)
        {
            Source = new VoxsSource {
                CellSize = .5f, AnchorInstance = -1,
                Palette = new NativeArray<PaletteEntry>(palette, Allocator.Persistent),
                Instances = new NativeArray<BlockInstance>(instances, Allocator.Persistent),
                BrickCoords = new NativeArray<int3>(bricks, Allocator.Persistent),
                Cells = new NativeArray<Cell>(bricks * 4096, Allocator.Persistent),
                RemovedBricks = new NativeArray<int3>(0, Allocator.Persistent)
            };
            for (int b = 0; b < bricks; b++) Source.BrickCoords[b] = new int3(-b, b % 3, b % 5);
        }
        internal static ushort Block(ContentCatalog c, string id) { c.TryGetBlockIndex(new ContentId(id), out ushort b); return b; }
        public static VoxsTestStructures Random(ContentCatalog catalog, uint seed)
        {
            var rng = RngStreams.For(seed, RngPurpose.Test, 0);
            int bricks = rng.NextInt(1, 41), palette = rng.NextInt(2, 301), instances = rng.NextInt(0, 101);
            var s = new VoxsTestStructures(bricks, palette, instances);
            for (int i = 1; i < palette; i++) s.Source.Palette[i] = new PaletteEntry { Block = Block(catalog, "block_hull"), Orient = (byte)rng.NextInt(24), Color = (byte)rng.NextInt(32) };
            for (int i = 0; i < s.Source.Cells.Length; i++)
            {
                if (rng.NextInt(4) == 0) continue;
                bool damaged = rng.NextInt(20) == 0;
                s.Source.Cells[i] = Cell.Palette((ushort)rng.NextInt(1, palette), damaged ? (byte)rng.NextInt(1, 256) : (byte)255, damaged ? (byte)rng.NextInt(256) : (byte)0);
            }
            for (int i = 0; i < instances; i++)
            {
                s.Source.Instances[i] = new BlockInstance { Block = Block(catalog, "block_multi"), Orient = (byte)rng.NextInt(24), Color = (byte)rng.NextInt(32), Origin = new int3(i, -i, 3), FunctionHp = 50, CellsAlive = 2 };
                s.Source.Cells[2 * i] = Cell.Instance(i); s.Source.Cells[2 * i + 1] = Cell.Instance(i);
            }
            s.Source.AnchorInstance = instances == 0 ? -1 : instances - 1;
            return s;
        }
        public static VoxsTestStructures ReferenceShip(ContentCatalog catalog, bool damaged = false)
        {
            var coords = new List<int3>(); var cells = new List<Cell[]>(); var surface = new List<(int, int)>();
            bool Inside(int3 p) { double x = (p.x + .5) / 40, y = (p.y + .5) / 25, z = (p.z + .5) / 240; return x*x + y*y + z*z <= 1; }
            var rng = RngStreams.For(1, RngPurpose.Test, 0);
            for (int z = -15; z < 15; z++) for (int y = -2; y < 2; y++) for (int x = -3; x < 3; x++)
            {
                int3 coord = new int3(x,y,z); var brick = new Cell[4096]; bool any = false;
                var edge = new List<int>();
                for (int n = 0; n < 4096; n++)
                {
                    int3 p = coord * 16 + new int3(n & 15, (n >> 4) & 15, n >> 8);
                    if (!Inside(p)) continue;
                    any = true; brick[n] = Cell.Palette((ushort)(1 + (p.z + 240) / 120), damaged && rng.NextInt(100) == 0 ? (byte)128 : (byte)255);
                    if (!Inside(p + new int3(0,0,1))) edge.Add(n);
                }
                if (!any) continue;
                foreach (int n in edge) surface.Add((coords.Count, n));
                coords.Add(coord); cells.Add(brick);
            }
            var s = new VoxsTestStructures(coords.Count, 5, 300);
            for (int i = 1; i <= 4; i++) s.Source.Palette[i] = new PaletteEntry { Block = Block(catalog,"block_hull"), Color = (byte)(i-1) };
            for (int i = 0; i < coords.Count; i++) { s.Source.BrickCoords[i] = coords[i]; s.Source.Cells.GetSubArray(i*4096,4096).CopyFrom(cells[i]); }
            for (int i = 0; i < 300; i++)
            {
                int chosen = rng.NextInt(i, surface.Count); var temp = surface[i]; surface[i] = surface[chosen]; surface[chosen] = temp;
                var (b,n) = surface[i];
                s.Source.Cells[b*4096+n] = Cell.Instance(i);
                s.Source.Instances[i] = new BlockInstance { Block = Block(catalog,"block_thruster_small"), Origin = coords[b]*16 + new int3(n&15,(n>>4)&15,n>>8), FunctionHp = 50, CellsAlive = 1 };
            }
            return s;
        }
        public static VoxsSource View(VoxsDecoded d) => new VoxsSource { CellSize=d.CellSize, AnchorInstance=d.AnchorInstance, Partial=d.Partial, Palette=d.Palette.AsArray(), Instances=d.Instances.AsArray(), BrickCoords=d.BrickCoords.AsArray(), Cells=d.Cells.AsArray(), RemovedBricks=d.RemovedBricks.AsArray() };
        public void Dispose()
        {
            Source.Palette.Dispose(); Source.Instances.Dispose(); Source.BrickCoords.Dispose(); Source.Cells.Dispose(); Source.RemovedBricks.Dispose();
        }
    }
}
