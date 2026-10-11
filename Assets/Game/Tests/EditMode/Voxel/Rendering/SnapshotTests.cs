using System;
using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using Random = Unity.Mathematics.Random;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    public sealed class SnapshotTests
    {
        ContentService content;
        [SetUp] public void SetUp() => content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
        [TearDown] public void TearDown() => content.Dispose();
        ushort Block(string id) => content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId(id)));
        VoxelContent VoxelContent => new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations };
        [DisableAutoCreation] partial struct LookupOwner : ISystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state) { }
        }
        sealed class SnapshotWorld : IDisposable
        {
            readonly World world;
            readonly SystemHandle handle;
            readonly ContentService content;
            public NativeArray<BrickMeshWork> Work;
            public NativeArray<uint> Snapshots;
            public NativeArray<BrickPropInstance> Props;
            public SnapshotWorld(VoxelStructure source, ContentService content)
            {
                this.content = content; world = new World("SnapshotTests"); var em = world.EntityManager;
                int3 min = source.Data.Bricks[0].Coord, max = min;
                foreach (var record in source.Data.Bricks) { min = math.min(min, record.Coord); max = math.max(max, record.Coord); }
                var grid = new VoxelGrid { BrickMin = min, BrickDims = max-min+1 };
                var root = em.CreateEntity(typeof(VoxelGrid), typeof(BrickSlot), typeof(PaletteEntry), typeof(BlockInstance)); em.SetComponentData(root, grid);
                em.GetBuffer<BrickSlot>(root).Resize(grid.BrickDims.x * grid.BrickDims.y * grid.BrickDims.z, NativeArrayOptions.ClearMemory);
                em.GetBuffer<PaletteEntry>(root).AddRange(source.Data.Palette.AsArray());
                em.GetBuffer<BlockInstance>(root).AddRange(source.Data.Instances.AsArray());
                int count = source.Data.Bricks.Length;
                Work = new NativeArray<BrickMeshWork>(count, Allocator.TempJob);
                Snapshots = new NativeArray<uint>(count * MeshCell.SnapCells, Allocator.TempJob);
                Props = new NativeArray<BrickPropInstance>(count * 512, Allocator.TempJob);
                for (int b = 0; b < count; b++)
                {
                    var record = source.Data.Bricks[b]; var brick = em.CreateEntity(typeof(Brick), typeof(BrickCells), typeof(BrickSparseCells));
                    em.SetComponentData(brick, new Brick { Grid = root, Coord = record.Coord, SolidCount = record.SolidCount });
                    var dense = em.GetBuffer<BrickCells>(brick); var sparse = em.GetBuffer<BrickSparseCells>(brick);
                    if (record.SolidCount > 1024) dense.ResizeUninitialized(4096);
                    for (int n = 0; n < 4096; n++)
                    {
                        Cell cell = source.GetCell(BrickMath.CellOf(record.Coord, n));
                        if (dense.Length != 0) dense[n] = new BrickCells { Value = cell };
                        else if (!cell.IsAir) sparse.Add(new BrickSparseCells { Local = (ushort)n, Value = cell });
                    }
                    var slots = em.GetBuffer<BrickSlot>(root);
                    slots[BrickMath.SlotIndex(record.Coord, min, grid.BrickDims)] = new BrickSlot { Brick = brick };
                    Work[b] = new BrickMeshWork { Brick = brick, Grid = root, PropCount = 999, SnapshotFlags = 255 };
                }
                handle = world.CreateSystem<LookupOwner>(); Run();
            }
            public void Run()
            {
                ref var state = ref world.Unmanaged.ResolveSystemStateRef(handle);
                new BrickSnapshotJob
                {
                    Bricks = state.GetComponentLookup<Brick>(true), Grids = state.GetComponentLookup<VoxelGrid>(true),
                    Slots = state.GetBufferLookup<BrickSlot>(true), Cells = state.GetBufferLookup<BrickCells>(true), Sparse = state.GetBufferLookup<BrickSparseCells>(true),
                    Palettes = state.GetBufferLookup<PaletteEntry>(true), Instances = state.GetBufferLookup<BlockInstance>(true),
                    Blocks = content.BlockDefs, Orientations = content.Orientations, Work = Work, Snapshots = Snapshots, Props = Props
                }.Schedule(Work.Length, 1).Complete();
            }
            public uint At(VoxelStructure source, int3 cell)
            {
                int b = source.Data.BrickIndex[BrickMath.BrickOf(cell)]; int3 p = BrickMath.LocalOf(cell);
                return Snapshots[b * MeshCell.SnapCells + MeshCell.SnapIndex(p.x,p.y,p.z)];
            }
            public void Dispose() { Props.Dispose(); Snapshots.Dispose(); Work.Dispose(); world.Dispose(); }
        }
        void Compare(VoxelStructure source, SnapshotWorld ecs)
        {
            using var expected = new NativeArray<uint>(MeshCell.SnapCells, Allocator.TempJob);
            for (int b = 0; b < source.Data.Bricks.Length; b++)
            {
                BrickSnapshotUtility.FromStructure(source.Data, source.Data.Bricks[b].Coord, ref content.BlockDefs.Value, ref content.Orientations.Value, expected);
                CollectionAssert.AreEqual(expected.ToArray(), ecs.Snapshots.GetSubArray(b * MeshCell.SnapCells, MeshCell.SnapCells).ToArray(), "brick " + source.Data.Bricks[b].Coord);
                Assert.AreEqual(0, ecs.Work[b].SnapshotFlags);
            }
        }
        [Test] public void EcsEqualsStructure()
        {
            using (var ship = BenchmarkStructures.Ship20k(Block("block_hull")))
            using (var ecs = new SnapshotWorld(ship, content)) Compare(ship, ecs);
            using var source = new VoxelStructure(); var rng = new Random(19);
            ushort palette = source.FindOrAddPalette(new PaletteEntry { Block = Block("block_hull_angled"), Orient = 5, Color = 201 });
            for (int z = -1; z <= 1; z++) for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            {
                int3 coord = new int3(x,y,z);
                if (math.all(coord == 0)) { source.SetBrickUniform(coord, Cell.Palette(palette)); continue; }
                bool dense = ((x+y+z)&1) == 0;
                for (int n = 0; n < 4096; n++) if (rng.NextInt(dense ? 2 : 32) == 0)
                    source.SetCell(BrickMath.CellOf(coord,n), Cell.Palette(palette, (byte)rng.NextInt(1,256), (byte)(rng.NextBool() ? CellFlags.Scorched : CellFlags.None)));
                // Every face/edge/corner of every neighbouring brick has a cell available to the halo.
                for (int c = 0; c < 8; c++) source.SetCell(coord*16 + new int3((c&1)*15,((c>>1)&1)*15,((c>>2)&1)*15), Cell.Palette(palette));
            }
            var voxelContent = VoxelContent;
            for (byte o = 0; o < 24; o++)
            {
                int3 origin = new int3(-8+4*(o%6), -8+4*(o/6), -8);
                int3 low = origin + content.Orientations.Value.Rotate(new int3(0,1,0), o);
                source.SetCell(origin, default); source.SetCell(low, default);
                Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = Block("block_hull_slope_long"), Origin = origin, Orient = o, Color = o }, voxelContent));
            }
            using var actual = new SnapshotWorld(source, content); Assert.AreEqual(27, actual.Work.Length); Compare(source,actual);
            for (byte o = 0; o < 24; o++)
            {
                int3 origin = new int3(-8+4*(o%6), -8+4*(o/6), -8);
                uint high = actual.At(source,origin), low = actual.At(source,origin + content.Orientations.Value.Rotate(new int3(0,1,0),o));
                Assert.AreEqual((int)BlockShape.LongSlopeHigh, MeshCell.Shape(high)); Assert.AreEqual((int)BlockShape.LongSlopeLow, MeshCell.Shape(low));
                Assert.AreEqual(o, MeshCell.Orient(high)); Assert.AreEqual(o, MeshCell.Colour(low));
            }
        }
        [Test] public void MissingNeighbours_Air()
        {
            using var source = new VoxelStructure(); ushort p = source.FindOrAddPalette(new PaletteEntry { Block = Block("block_hull") });
            source.SetBrickUniform(new int3(-1), Cell.Palette(p));
            using var actual = new SnapshotWorld(source, content); Compare(source, actual);
            for (int z=-1; z<=16; z++) for (int y=-1; y<=16; y++) for (int x=-1; x<=16; x++)
            {
                bool border = x==-1 || x==16 || y==-1 || y==16 || z==-1 || z==16;
                Assert.AreEqual(border ? 0 : 1, MeshCell.Class(actual.Snapshots[MeshCell.SnapIndex(x,y,z)]));
            }
        }
        [Test] public void BadRef_MissingLook()
        {
            using var source = new VoxelStructure();
            source.Data.Palette.Add(new PaletteEntry { Block = ushort.MaxValue }); source.Data.Palette.Add(default);
            source.Data.Palette.Add(new PaletteEntry { Block = Block("block_hull"), Orient = 24 });
            source.Data.Instances.Add(new BlockInstance { Block = Block("block_hull_slope_long"), Origin = new int3(1000), Orient = 0 });
            var cells = new[] { Cell.Palette(32767), Cell.Instance(32767), Cell.Palette(1), Cell.Palette(2), Cell.Palette(3), Cell.Instance(0) };
            for (int i=0; i<cells.Length; i++) { var c=cells[i]; c.Hp=63; c.Flags=(byte)CellFlags.Scorched; source.SetCell(new int3(i,0,0),c); }
            using var actual = new SnapshotWorld(source,content);
            Assert.AreEqual(1, actual.Work[0].SnapshotFlags); Assert.AreEqual(0,actual.Work[0].PropCount);
            using var expected = new NativeArray<uint>(MeshCell.SnapCells,Allocator.TempJob);
            BrickSnapshotUtility.FromStructure(source.Data,int3.zero,ref content.BlockDefs.Value,ref content.Orientations.Value,expected);
            for (int i=0; i<cells.Length; i++)
            {
                uint c=actual.At(source,new int3(i,0,0)); Assert.AreEqual(expected[MeshCell.SnapIndex(i,0,0)],c);
                Assert.AreEqual(1,MeshCell.Class(c)); Assert.AreEqual(0,MeshCell.Shape(c)); Assert.AreEqual(0,MeshCell.Orient(c));
                Assert.AreEqual(63,MeshCell.Layer(c)); Assert.AreEqual(0,MeshCell.Colour(c)); Assert.AreEqual(3,MeshCell.Damage(c)); Assert.IsTrue(MeshCell.Scorched(c));
            }
        }
        [Test] public void Props_Collected()
        {
            using var source = new VoxelStructure(); ushort prop=Block("block_portal_small");
            ushort palette=source.FindOrAddPalette(new PaletteEntry { Block=prop, Orient=4, Color=23 });
            source.SetCell(new int3(-1,0,0),Cell.Palette(palette)); // halo root: belongs to its own brick only
            for (int n=0;n<513;n++) source.SetCell(BrickMath.LocalFromIndex(n),Cell.Palette(palette));
            // An instance root of a prop is collected; functional roots and non-root instance cells are not.
            source.Data.Instances.Add(new BlockInstance { Block=prop, Origin=new int3(1,2,3), Orient=4, Color=44 });
            source.SetCell(new int3(1,2,3),Cell.Instance(0,255,(byte)CellFlags.InstanceRoot));
            var voxelContent=VoxelContent;
            Assert.AreEqual(PlaceResult.Ok,source.TryPlaceBlock(new BlockPlacement { Block=Block("block_core_small"), Origin=new int3(2,2,3) },voxelContent));
            source.Data.Instances.Add(new BlockInstance { Block=prop, Origin=new int3(3,2,3) });
            source.SetCell(new int3(3,2,3),Cell.Instance(source.Data.Instances.Length-1));
            using (var overflow = new SnapshotWorld(source,content))
            {
                int b=source.Data.BrickIndex[int3.zero]; Assert.AreEqual(512,overflow.Work[b].PropCount); Assert.AreEqual(2,overflow.Work[b].SnapshotFlags);
                for (int k=0;k<512;k++) Assert.AreEqual(k,overflow.Props[overflow.Work[b].PropStart+k].LocalIndex);
            }
            for (int n=1;n<513;n++) source.SetCell(BrickMath.LocalFromIndex(n),default);
            using var actual=new SnapshotWorld(source,content); int center=source.Data.BrickIndex[int3.zero]; var work=actual.Work[center];
            Assert.AreEqual(2,work.PropCount); Assert.AreEqual(0,work.SnapshotFlags);
            Assert.AreEqual(0,actual.Props[work.PropStart].LocalIndex);
            var root=actual.Props[work.PropStart+1]; Assert.AreEqual(BrickMath.LocalIndex(1,2,3),root.LocalIndex);
            Assert.AreEqual(prop,root.Block); Assert.AreEqual(4,root.Orient); Assert.AreEqual(44,root.Color);
            int outside=source.Data.BrickIndex[new int3(-1,0,0)]; Assert.AreEqual(1,actual.Work[outside].PropCount);
        }
    }
}
