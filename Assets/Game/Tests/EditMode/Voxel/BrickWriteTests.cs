using Game.Data.Blocks;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-012: cell write bookkeeping and Burst lookup reads (SPEC-10 §4.6/§6).</summary>
    public sealed class BrickWriteTests
    {
        World world;
        Entity entity;
        EntityManager Manager => world.EntityManager;
        DynamicBuffer<BrickSparseCells> Sparse => Manager.GetBuffer<BrickSparseCells>(entity);
        DynamicBuffer<BrickCells> Cells => Manager.GetBuffer<BrickCells>(entity);
        DynamicBuffer<BrickSolidMask> Mask => Manager.GetBuffer<BrickSolidMask>(entity);

        [SetUp]
        public void SetUp()
        {
            world = new World("BrickWriteTests");
            entity = Manager.CreateEntity(typeof(BrickCells), typeof(BrickSparseCells), typeof(BrickSolidMask));
            Manager.GetBuffer<BrickCells>(entity).Resize(4096, NativeArrayOptions.ClearMemory);
            Manager.GetBuffer<BrickSolidMask>(entity).Resize(64, NativeArrayOptions.ClearMemory);
        }
        [TearDown] public void TearDown() => world.Dispose();

        [Test]
        public void Flags_PerWriteKind()
        {
            var brick = new Brick(); var dirty = new BrickDirty();
            int local = BrickMath.LocalIndex(3, 4, 5);
            Assert.IsTrue(BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, local, Cell.Palette(1), 8));
            Assert.AreEqual(1, brick.SolidCount); Assert.AreEqual(8, brick.ChangedRevision);
            Assert.AreEqual(BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider, dirty.Flags);
            Assert.IsTrue((Mask[local >> 6].Bits & (1UL << (local & 63))) != 0);
            dirty = default;
            Assert.IsTrue(BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, local, Cell.Palette(1, 240), 9));
            Assert.AreEqual(BrickDirtyFlags.Mesh, dirty.Flags); Assert.AreEqual(1, brick.SolidCount);
            Assert.AreEqual(BlockFaceMask.None, dirty.BorderMask);
            dirty = default;
            BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, local, Cell.Palette(1, 127), 10);
            Assert.AreEqual(BrickDirtyFlags.Mesh, dirty.Flags); Assert.AreEqual(BlockFaceMask.None, dirty.BorderMask);
            dirty = default;
            BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, local, Cell.Instance(4, 127, (byte)CellFlags.Scorched), 11);
            Assert.AreEqual(BrickDirtyFlags.Mesh, dirty.Flags); Assert.AreEqual(1, brick.SolidCount);
            dirty = default;
            Assert.IsTrue(BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, local, default, 12));
            Assert.AreEqual(BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider, dirty.Flags);
            Assert.AreEqual(0, brick.SolidCount); Assert.AreEqual(12, brick.ChangedRevision);
            Assert.AreEqual(0, Mask[local >> 6].Bits); Assert.AreEqual(default(Cell), BrickWrite.Get(Cells, Sparse, local));
        }

        [Test]
        public void BorderMask_OnEdgeCells()
        {
            var brick = new Brick(); var dirty = new BrickDirty();
            for (int z = 0; z < 16; z++) for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                int local = BrickMath.LocalIndex(x, y, z);
                BlockFaceMask expected = (x == 0 ? BlockFaceMask.NegX : x == 15 ? BlockFaceMask.PosX : 0)
                    | (y == 0 ? BlockFaceMask.NegY : y == 15 ? BlockFaceMask.PosY : 0)
                    | (z == 0 ? BlockFaceMask.NegZ : z == 15 ? BlockFaceMask.PosZ : 0);
                dirty = default; BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, local, Cell.Palette(1), 1);
                Assert.AreEqual(expected, dirty.BorderMask);
                dirty = default; BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, local, Cell.Palette(1, 240), 2);
                Assert.AreEqual(BlockFaceMask.None, dirty.BorderMask);
                dirty = default; BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, local, Cell.Palette(1, 180), 3);
                Assert.AreEqual(expected, dirty.BorderMask);
                dirty = new BrickDirty { BorderMask = BlockFaceMask.PosY };
                BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, local, default, 4);
                Assert.AreEqual(expected | BlockFaceMask.PosY, dirty.BorderMask);
            }
        }

        [Test]
        public void NoOp_ReturnsFalse()
        {
            var brick = new Brick { Coord = new int3(-3, 2, 1), ChangedRevision = 17 };
            var dirty = new BrickDirty { Flags = BrickDirtyFlags.Collider, BorderMask = BlockFaceMask.NegX };
            Assert.IsFalse(BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, 13, default, 18));
            Assert.AreEqual(17, brick.ChangedRevision); Assert.AreEqual(0, brick.SolidCount);
            Assert.AreEqual(BrickDirtyFlags.Collider, dirty.Flags); Assert.AreEqual(BlockFaceMask.NegX, dirty.BorderMask);
            BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, 13, Cell.Palette(3, 96, 2), 19);
            var previousBrick = brick; var previousDirty = dirty; ulong previousMask = Mask[0].Bits;
            Assert.IsFalse(BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, 13, Cell.Palette(3, 96, 2), 20));
            Assert.AreEqual(previousBrick, brick); Assert.AreEqual(previousDirty, dirty); Assert.AreEqual(previousMask, Mask[0].Bits);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct WriteJob : IJob
        {
            public DynamicBuffer<BrickCells> Cells;
            public DynamicBuffer<BrickSparseCells> Sparse;
            public DynamicBuffer<BrickSolidMask> Mask;
            public NativeArray<Brick> Bricks;
            public NativeArray<BrickDirty> Dirties;
            public void Execute()
            {
                var brick = Bricks[0]; var dirty = Dirties[0];
                BrickWrite.Set(ref brick, Cells, Sparse, Mask, ref dirty, 15, Cell.Palette(1, 63), 42);
                Bricks[0] = brick; Dirties[0] = dirty;
            }
        }
        [DisableAutoCreation]
        partial struct LookupOwnerSystem : ISystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state) { }
        }
        [BurstCompile(CompileSynchronously = true)]
        struct ReadJob : IJob
        {
            public GridCellReader Reader;
            public Entity Root;
            public NativeArray<int> Result;
            public void Execute()
            {
                int3 cell = new int3(-1, 0, 0);
                Result[0] = Reader.TryGetCell(Root, cell, out var value) ? value.Ref : 0;
                Result[1] = Reader.IsSolid(Root, cell) ? 1 : 0;
                Result[2] = Reader.TryGetCell(Root, new int3(-2, 0, 0), out _) ? 1 : 0;
                Result[3] = Reader.GetBrick(Root, new int3(-2, 0, 0)) == Entity.Null ? 1 : 0;
                Result[4] = Reader.IsSolid(Root, default) ? 1 : 0;
                Result[5] = Reader.TryGetCell(Entity.Null, cell, out _) ? 1 : 0;
            }
        }

        [Test]
        public void BurstJob_Runs()
        {
            bool safety = BurstCompiler.Options.EnableBurstSafetyChecks;
            bool compilation = BurstCompiler.Options.EnableBurstCompilation;
            try
            {
                BurstCompiler.Options.EnableBurstSafetyChecks = true;
                BurstCompiler.Options.EnableBurstCompilation = true;
                using var bricks = new NativeArray<Brick>(1, Allocator.TempJob);
                using var dirties = new NativeArray<BrickDirty>(1, Allocator.TempJob);
                new WriteJob { Cells = Cells, Sparse = Sparse, Mask = Mask, Bricks = bricks, Dirties = dirties }.Schedule().Complete();
                Assert.AreEqual(1, bricks[0].SolidCount); Assert.AreEqual(42, bricks[0].ChangedRevision);
                Assert.AreEqual(BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider, dirties[0].Flags);
                Assert.AreEqual(BlockFaceMask.PosX | BlockFaceMask.NegY | BlockFaceMask.NegZ, dirties[0].BorderMask);
                var root = Manager.CreateEntity(typeof(VoxelGrid), typeof(BrickSlot));
                Manager.SetComponentData(root, new VoxelGrid { BrickMin = new int3(-1, 0, 0), BrickDims = new int3(2, 1, 1) });
                var slots = Manager.GetBuffer<BrickSlot>(root); slots.Resize(2, NativeArrayOptions.ClearMemory);
                slots[0] = new BrickSlot { Brick = entity };
                var handle = world.CreateSystem<LookupOwnerSystem>();
                ref var state = ref world.Unmanaged.ResolveSystemStateRef(handle);
                var reader = GridCellReader.Create(ref state);
                reader.Update(ref state);
                using var result = new NativeArray<int>(6, Allocator.TempJob);
                new ReadJob { Reader = reader, Root = root, Result = result }.Schedule().Complete();
                CollectionAssert.AreEqual(new[] { 1, 1, 0, 1, 0, 0 }, result.ToArray());
            }
            finally
            {
                BurstCompiler.Options.EnableBurstSafetyChecks = safety;
                BurstCompiler.Options.EnableBurstCompilation = compilation;
            }
        }
    }
}
