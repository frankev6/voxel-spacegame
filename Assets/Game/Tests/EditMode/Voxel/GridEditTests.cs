using System;
using Game.Core.Components;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-020: the three specified preview checks against the real draft and spawn owner.</summary>
    public sealed class GridEditTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly VoxelTestContent Content = new VoxelTestContent();
            public readonly VoxelStructure Draft = new VoxelStructure();
            public readonly World World = new World("Preview edits");
            readonly BlueprintLibrary library;
            readonly VoxelStructureRegistry registry;
            readonly SystemHandle spawn;
            public Entity Root;
            public EntityManager Em => World.EntityManager;
            public Fixture()
            {
                library = new BlueprintLibrary(Content.Service.Catalog, Content.Content);
                registry = VoxelStructureRegistry.CreateRuntime(library);
                Content.Service.CreateSingletons(Em); Em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                Em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024 }); Em.CreateEntity(typeof(GameplayActive));
                spawn = World.CreateSystem<GridSpawnSystem>();
            }
            public void Place(string block, int3 cell) => Assert.AreEqual(PlaceResult.Ok,
                Draft.TryPlaceBlock(new BlockPlacement { Block = Content.Index(block), Origin = cell }, Content.Content));
            public void Spawn()
            {
                Root = GridSpawnApi.RequestSpawn(Em, registry, registry.Register(Draft), new GridSpawnParams
                { Pose = new RigidTransform(quaternion.RotateY(.7f), new float3(3, 0, 9)), Flags = GridSpawnFlags.EditorPreview | GridSpawnFlags.NoRender });
                spawn.Update(World.Unmanaged); Assert.IsFalse(Draft.IsPinned);
            }
            public void Edit(params int3[] coords)
            {
                using var changed = new NativeArray<int3>(coords, Allocator.Temp);
                GridSpawnApi.RequestEdit(Em, Root, changed); spawn.Update(World.Unmanaged);
                Assert.IsFalse(Em.IsComponentEnabled<GridEditRequest>(Root)); Assert.AreEqual(0, Em.GetBuffer<GridEditedBrick>(Root).Length);
                Assert.IsFalse(Draft.IsPinned);
            }
            public Entity Brick(int3 coord)
            {
                var grid = Em.GetComponentData<VoxelGrid>(Root); int slot = BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims);
                return slot < 0 ? Entity.Null : Em.GetBuffer<BrickSlot>(Root)[slot].Brick;
            }
            public void Match()
            {
                int actual = 0;
                foreach (var slot in Em.GetBuffer<BrickSlot>(Root)) if (slot.Brick != Entity.Null) actual++;
                Assert.AreEqual(Draft.BrickCount, actual);
                foreach (var record in Draft.Data.Bricks)
                {
                    var entity = Brick(record.Coord); Assert.AreNotEqual(Entity.Null, entity);
                    Assert.AreEqual(record.SolidCount, Em.GetComponentData<Brick>(entity).SolidCount);
                    var cells = Em.GetBuffer<BrickCells>(entity); var sparse = Em.GetBuffer<BrickSparseCells>(entity);
                    int mismatch = -1;
                    for (int i = 0; i < 4096; i++)
                        if (!BrickWrite.Get(cells, sparse, i).Equals(Draft.GetCell((record.Coord << 4) + BrickMath.LocalFromIndex(i)))) { mismatch = i; break; }
                    Assert.AreEqual(-1, mismatch, "Copied cells must exactly match the draft.");
                    var mask = Em.GetBuffer<BrickSolidMask>(entity); var expected = Draft.Data.Masks[Draft.Data.BrickIndex[record.Coord]];
                    unsafe { for (int i = 0; i < mask.Length; i++) Assert.AreEqual(expected.Words[i], mask[i].Bits); }
                }
            }
            public void Dispose() { World.Dispose(); Object.DestroyImmediate(registry); Draft.Dispose(); library.Dispose(); Content.Dispose(); }
        }
        [Test] public void MirroredEdits_MatchDraft()
        {
            using var f = new Fixture(); f.Place("block_hull", new int3(100, 0, 0)); f.Spawn();
            var rng = new Unity.Mathematics.Random(10203);
            for (int i = 0; i < 1000; i++)
            {
                var cell = new int3(rng.NextInt(-16, 32), 0, rng.NextInt(0, 32));
                if (f.Draft.GetCell(cell).IsAir) f.Place("block_hull", cell);
                else Assert.IsTrue(f.Draft.TryRemoveBlockAt(cell, f.Content.Content, out _));
                var coord = BrickMath.BrickOf(cell); uint revision = f.Em.GetComponentData<VoxelGrid>(f.Root).Revision;
                f.Edit(coord, coord); Assert.AreEqual(revision + 1, f.Em.GetComponentData<VoxelGrid>(f.Root).Revision); f.Match();
            }
        }
        [Test] public void BoundsGrowth_RemapsSlots()
        {
            using var f = new Fixture(); f.Place("block_hull", int3.zero); f.Spawn();
            Entity original = f.Brick(int3.zero); var pose = f.Em.GetComponentData<LocalTransform>(f.Root);
            f.Place("block_hull", new int3(-33, -17, -49)); f.Place("block_hull", new int3(65, 32, 17));
            f.Edit(new int3(4, 2, 1), new int3(-3, -2, -4));
            var grid = f.Em.GetComponentData<VoxelGrid>(f.Root);
            Assert.AreEqual(new int3(-3, -2, -4), grid.BrickMin); Assert.AreEqual(new int3(8, 5, 6), grid.BrickDims);
            Assert.AreEqual(original, f.Brick(int3.zero)); Assert.AreEqual(pose, f.Em.GetComponentData<LocalTransform>(f.Root)); f.Match();
            Assert.IsTrue(f.Draft.TryRemoveBlockAt(new int3(-33, -17, -49), f.Content.Content, out _));
            f.Edit(new int3(-3, -2, -4)); Assert.AreEqual(Entity.Null, f.Brick(new int3(-3, -2, -4))); f.Match();
        }
        [Test] public void FunctionalAddRemove()
        {
            using var f = new Fixture(); f.Place("block_hull", int3.zero); f.Place("block_core_cross", new int3(4, 0, 4)); f.Spawn();
            Entity core = f.Em.GetBuffer<BlockInstance>(f.Root)[0].Functional; Assert.IsTrue(f.Em.Exists(core));
            f.Place("block_core_cross", new int3(15, 0, 15));
            f.Edit(new int3(1, 0, 1), new int3(0, 0, 1), new int3(1, 0, 0), int3.zero);
            Entity added = f.Em.GetBuffer<BlockInstance>(f.Root)[1].Functional;
            Assert.IsTrue(f.Em.Exists(added)); Assert.AreEqual(core, f.Em.GetBuffer<BlockInstance>(f.Root)[0].Functional);
            Assert.AreEqual(f.Root, f.Em.GetComponentData<FunctionalBlock>(added).Grid); f.Match();
            Assert.IsTrue(f.Draft.TryRemoveBlockAt(new int3(15, 0, 15), f.Content.Content, out _));
            f.Edit(int3.zero, new int3(1, 0, 0), new int3(0, 0, 1), new int3(1, 0, 1));
            Assert.IsFalse(f.Em.Exists(added)); Assert.IsTrue(f.Em.Exists(core)); f.Match();
            f.Place("block_core_cross", new int3(15, 0, 15)); f.Edit(int3.zero, new int3(1, 0, 0), new int3(0, 0, 1));
            Entity replacement = f.Em.GetBuffer<BlockInstance>(f.Root)[1].Functional;
            Assert.IsTrue(f.Em.Exists(replacement)); Assert.AreNotEqual(added, replacement); f.Match();
        }
    }
}
