using Game.App.DevRuntime;
using Game.App.DevRuntime.Overlays;
using Game.Core.Components;
using Game.Simulation.Commands;
using Game.Simulation.Spatial;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
namespace Game.Tests.EditMode.App
{
    /// <summary>The three DEV-013 cases, with tiny committed masks and explicitly advanced cache draws.</summary>
    public sealed class IslandOverlayTests : GameTestBase
    {
        static Entity Grid(EntityManager em)
        {
            var root = em.CreateEntity(typeof(VoxelGrid), typeof(LocalToWorld), typeof(PersistentId), typeof(PlayerShip), typeof(BrickSlot));
            em.SetComponentData(root, new VoxelGrid { BrickDims = new int3(1), CellSize = 1, Serial = 1, CellMax = new int3(6, 2, 2) });
            em.SetComponentData(root, new LocalToWorld { Value = float4x4.identity });
            em.SetComponentData(root, new PersistentId(1234));
            var brick = em.CreateEntity(typeof(Brick), typeof(BrickCells), typeof(BrickSparseCells), typeof(BrickSolidMask), typeof(BrickDirty));
            em.SetComponentData(brick, new Brick { Grid = root });
            em.GetBuffer<BrickSolidMask>(brick).Resize(BrickMath.MaskWords, NativeArrayOptions.ClearMemory);
            em.GetBuffer<BrickSlot>(root).Add(new BrickSlot { Brick = brick });
            for (int z = 0; z < 3; z++)
                for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++)
                    { Solid(em, brick, new int3(x, y, z)); Solid(em, brick, new int3(x + 4, y, z)); }
            return root;
        }
        static void Solid(EntityManager em, Entity entity, int3 cell)
        {
            var brick = em.GetComponentData<Brick>(entity); var dirty = em.GetComponentData<BrickDirty>(entity);
            BrickWrite.Set(ref brick, em.GetBuffer<BrickCells>(entity), em.GetBuffer<BrickSparseCells>(entity),
                em.GetBuffer<BrickSolidMask>(entity), ref dirty, BrickMath.LocalIndex(cell), Cell.Palette(1), 1);
            em.SetComponentData(entity, brick); em.SetComponentData(entity, dirty);
        }
        [Test] public void TwoBlocks_TwoIslands()
        {
            var world = CreateWorld(); var em = world.EntityManager; var root = Grid(em);
            var host = DevRuntimeHost.Create(null);
            try
            {
                var command = host.Commands.Execute("overlay name=islands id=1234", host.Context);
                Assert.AreEqual(DevResultKind.Ok, command.Kind);
                var overlay = host.Overlays; Assert.AreEqual(1234UL, overlay.IslandTargetId);
                overlay.Gather(world.World, float3.zero);
                Assert.AreEqual(48, overlay.Lines.Vertices.Length);
                StringAssert.Contains("islands=2", host.Commands.Execute("overlay", host.Context).Text);
                Assert.AreEqual(new Color32(255, 255, 255, 255), overlay.Lines.Vertices[0].Color);
                Assert.AreEqual(new Color32(0, 74, 255, 255), overlay.Lines.Vertices[24].Color);
                Assert.AreEqual(float3.zero, overlay.Lines.Vertices[0].Position);
                Assert.AreEqual(new float3(4, 0, 0), overlay.Lines.Vertices[24].Position);
                em.SetComponentData(root, new LocalToWorld { Value = float4x4.Translate(new float3(10, 5, 0)) });
                overlay.Gather(world.World, float3.zero);
                Assert.AreEqual(new float3(10, 5, 0), overlay.Lines.Vertices[0].Position, "Cached bounds follow the current pose.");

                // Draw real occupied hash buckets, including a negative cell and excluding the bucket beyond 1 km.
                var hash = SpatialHash.Create(128); var singleton = em.CreateEntity(typeof(SpatialHash));
                try
                {
                    hash.Proxies.Add(new SpatialProxy { Entity = root, Center = new float3(1, 500, 1) });
                    hash.Proxies.Add(new SpatialProxy { Entity = root, Center = new float3(-1, 0, 1) });
                    hash.Proxies.Add(new SpatialProxy { Entity = root, Center = new float3(1025, 0, 1) });
                    var writer = new ProxyBucketJob { Proxies = hash.Proxies, Large = hash.Large, EntryIndices = hash.EntryIndices,
                        Cells = hash.Cells, State = hash.State, CellSize = 128 }.Schedule(hash.Access.Acquire(default));
                    hash.Access.Release(writer); em.SetComponentData(singleton, hash);
                    host.Commands.Execute("overlay name=none", host.Context);
                    Assert.AreEqual(DevResultKind.Ok, host.Commands.Execute("overlay name=spatial", host.Context).Kind);
                    overlay.Gather(world.World, float3.zero);
                    Assert.AreEqual(16, overlay.Lines.Vertices.Length);
                    foreach (var vertex in overlay.Lines.Vertices) Assert.AreEqual(0, vertex.Position.y);
                    StringAssert.Contains("spatial cells=2", overlay.Status);
                }
                finally { em.DestroyEntity(singleton); hash.Dispose(); }
            }
            finally { host.Dispose(); }
        }
        [Test] public void Joined_OneIsland()
        {
            var world = CreateWorld(); var em = world.EntityManager; var root = Grid(em);
            using var overlay = new DevOverlayRenderer { Enabled = DevOverlayFlags.Islands };
            overlay.Gather(world.World, float3.zero); Assert.AreEqual(48, overlay.Lines.Vertices.Length);
            var brick = em.GetBuffer<BrickSlot>(root)[0].Brick;
            Solid(em, brick, new int3(3, 1, 1));
            Assert.AreEqual(55, em.GetComponentData<Brick>(brick).SolidCount);
            for (int frame = 1; frame < 60; frame++)
            { overlay.Gather(world.World, float3.zero); Assert.AreEqual(48, overlay.Lines.Vertices.Length, "Cached frame " + frame); }
            overlay.Gather(world.World, float3.zero);
            Assert.AreEqual(24, overlay.Lines.Vertices.Length); StringAssert.Contains("islands=1", overlay.Status);
            Assert.AreEqual(new Color32(255, 255, 255, 255), overlay.Lines.Vertices[0].Color);
            float3 max = float3.zero;
            foreach (var vertex in overlay.Lines.Vertices) max = math.max(max, vertex.Position);
            Assert.AreEqual(new float3(7, 3, 3), max);
        }
        [Test] public void TooLarge_Skipped()
        {
            var world = CreateWorld(); var em = world.EntityManager;
            var root = em.CreateEntity(typeof(VoxelGrid), typeof(LocalToWorld), typeof(PlayerShip));
            em.SetComponentData(root, new VoxelGrid { CellSize = 1, CellMax = new int3(IslandOverlayJob.MaxCells, 0, 0) });
            em.SetComponentData(root, new LocalToWorld { Value = float4x4.identity });
            using var overlay = new DevOverlayRenderer { Enabled = DevOverlayFlags.Islands };
            overlay.Gather(world.World, float3.zero);
            Assert.AreEqual(0, overlay.Lines.Vertices.Length);
            StringAssert.Contains("grid too large for islands overlay", overlay.Status);
            // Check the exact cap and overflow arithmetic without allocating or scanning a huge fixture.
            Assert.AreEqual(IslandOverlayJob.MaxCells, IslandOverlayJob.CellCount(int3.zero, new int3(IslandOverlayJob.MaxCells - 1, 0, 0)));
            Assert.AreEqual(-1, IslandOverlayJob.CellCount(new int3(int.MinValue), new int3(int.MaxValue)));
        }
    }
}
