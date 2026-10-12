using System;
using System.Diagnostics;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Transforms;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    /// <summary>REN-012: distance hysteresis, conditional writes, asynchronous mesh catch-up and advisory scheduling cost.</summary>
    public sealed class GridLodSystemTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly EcsTestWorld World = EcsTestWorld.Create(typeof(GridLodSystem));
            public EntityManager EM => World.EntityManager;
            public readonly Entity State, Grid, Brick;
            public Fixture(int count = 1)
            {
                World.AddGameplayActive();
                State = EM.CreateEntity(typeof(VoxelViewer), typeof(VoxelRenderConfig), typeof(BrickMeshQueue));
                EM.SetComponentData(State, new VoxelRenderConfig { Lod1Distance = 400, DormantDistance = 3000, LodHysteresis = .1f });
                EM.SetComponentData(State, BrickMeshQueue.Create());
                Grid = EM.CreateEntity(typeof(VoxelGrid), typeof(LocalToWorld), typeof(Dormant));
                EM.SetComponentData(Grid, new VoxelGrid { CellMin = 0, CellMax = new int3(15), CellSize = 1 });
                EM.SetComponentData(Grid, new LocalToWorld { Value = float4x4.identity });
                EM.SetComponentEnabled<Dormant>(Grid, false);
                var archetype = EM.CreateArchetype(typeof(Brick), typeof(LocalToWorld), typeof(BrickLodDesired), typeof(BrickRenderState));
                using var entities = new NativeArray<Entity>(count, Allocator.Temp);
                EM.CreateEntity(archetype, entities); Brick = entities[0];
                foreach (var entity in entities)
                {
                    EM.SetComponentData(entity, new Brick { Grid = Grid, SolidCount = 1 });
                    EM.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
                    EM.SetComponentData(entity, new BrickRenderState { HasMesh = 1 });
                }
                Viewer(0, 0);
            }
            public BrickMeshQueue Queue => EM.GetComponentData<BrickMeshQueue>(State);
            public byte Desired => EM.GetComponentData<BrickLodDesired>(Brick).Lod;
            public bool Dormant => EM.IsComponentEnabled<Dormant>(Grid);
            public void Viewer(float cameraDistance, float anchorDistance, byte valid = 1, float scale = 1)
            {
                float3 center = new float3(8 * scale); float radius = 8 * math.sqrt(3f) * scale;
                EM.SetComponentData(State, new VoxelViewer { Valid = valid,
                    CameraPosition = center + new float3(0, 0, radius + cameraDistance),
                    AnchorPosition = center + new float3(0, 0, radius + anchorDistance) });
            }
            public void Tick() => World.StepFrame(0);
            public void Shown(byte lod) => EM.SetComponentData(Brick, new BrickRenderState { HasMesh = 1, ShownLod = lod });
            public void Drain()
            {
                var q = Queue; q.CompleteAccess();
                while (q.TryDequeue(out var request)) Assert.AreEqual(MeshPriority.Lod, request.Priority);
            }
            public void Dispose()
            {
                World.CompleteAll(); Queue.Dispose(); EM.DestroyEntity(State); World.Dispose();
            }
        }
        [Test] public void Lod_Hysteresis()
        {
            using var f = new Fixture(); f.Viewer(399, 0); f.Tick(); Assert.AreEqual(0, f.Desired);
            f.Viewer(401, 0); f.Tick(); Assert.AreEqual(1, f.Desired); Assert.AreEqual(1, f.Queue.Count);
            f.Drain(); f.Shown(1); f.Viewer(380, 0); f.Tick(); Assert.AreEqual(1, f.Desired); Assert.AreEqual(0, f.Queue.Count);
            f.Viewer(359, 0); f.Tick(); Assert.AreEqual(0, f.Desired); Assert.AreEqual(1, f.Queue.Count);
            f.Drain(); f.Shown(0); f.Tick();
            // A requested LOD1 starts building, then the camera returns before its apply.
            f.Viewer(401, 0); f.Tick(); f.Drain();
            f.Viewer(350, 0); f.Tick(); Assert.AreEqual(0, f.Queue.Count); Assert.AreEqual(0, f.Desired);
            f.Shown(1); f.Tick(); Assert.AreEqual(1, f.Queue.Count, "Late opposite-LOD apply must rebuild even though desired did not change");
        }
        [Test] public void Dormant_Hysteresis()
        {
            using var f = new Fixture();
            foreach (float scale in new[] { 1f, 6f })
            {
                // CellSize 2 and root scale 3 compose to six; full inclusive bounds still measure the same surface distance.
                var grid = f.EM.GetComponentData<VoxelGrid>(f.Grid); grid.CellSize = scale == 1 ? 1 : 2;
                f.EM.SetComponentData(f.Grid, grid);
                f.EM.SetComponentData(f.Grid, new LocalToWorld { Value = float4x4.Scale(scale == 1 ? 1 : 3) });
                f.EM.SetComponentData(f.Brick, new LocalToWorld { Value = float4x4.Scale(scale) });
                f.Viewer(0, 2999, scale: scale); f.Tick(); Assert.IsFalse(f.Dormant);
                f.Viewer(0, 3001, scale: scale); f.Tick(); Assert.IsTrue(f.Dormant);
                f.Viewer(0, 2800, scale: scale); f.Tick(); Assert.IsTrue(f.Dormant);
                f.Viewer(0, 2699, scale: scale); f.Tick(); Assert.IsFalse(f.Dormant);
            }
        }
        [Test] public void Dormant_ForcesLod1()
        {
            using var f = new Fixture(); f.Viewer(0, 3001); f.Tick();
            Assert.IsTrue(f.Dormant); Assert.AreEqual(1, f.Desired); Assert.AreEqual(1, f.Queue.Count);
            f.Drain(); f.Shown(1); f.Viewer(0, 2800); f.Tick(); Assert.AreEqual(1, f.Desired);
            f.Viewer(0, 2699); f.Tick(); Assert.AreEqual(0, f.Desired); Assert.AreEqual(1, f.Queue.Count);
        }
        static uint Version<T>(EntityManager em, Entity entity) where T : unmanaged, IComponentData
        {
            var type = em.GetComponentTypeHandle<T>(true);
            return em.GetChunk(entity).GetChangeVersion(ref type);
        }
        [Test] public void Idle_NoWrites()
        {
            using var f = new Fixture();
            foreach (float distance in new[] { 0f, 4000f })
            {
                f.Viewer(distance, distance); f.Tick(); f.Drain(); f.Shown(f.Desired); f.Tick();
                uint desired = Version<BrickLodDesired>(f.EM, f.Brick);
                uint dormant = Version<Dormant>(f.EM, f.Grid);
                for (int i = 0; i < 3; i++) f.Tick();
                Assert.AreEqual(desired, Version<BrickLodDesired>(f.EM, f.Brick));
                Assert.AreEqual(dormant, Version<Dormant>(f.EM, f.Grid));
                Assert.AreEqual(0, f.Queue.Count);
            }
        }
        [Test] public void InvalidViewer_AllLod0()
        {
            using var f = new Fixture(); f.Viewer(4000, 4000); f.Tick(); f.Drain(); f.Shown(1);
            f.Viewer(4000, 4000, valid: 0); f.Tick();
            Assert.IsFalse(f.Dormant); Assert.AreEqual(0, f.Desired); Assert.AreEqual(1, f.Queue.Count, "Shown LOD1 is queued back to LOD0");
            f.Drain(); f.Shown(0); f.Tick(); Assert.AreEqual(0, f.Queue.Count);
        }
        [Test, Performance, Category("Performance")] public void MainThreadCost5000()
        {
            using var f = new Fixture(5000); for (int i = 0; i < 10; i++) f.Tick();
            var samples = new double[30];
            for (int i = 0; i < samples.Length; i++)
            {
                long start = Stopwatch.GetTimestamp(); f.World.UpdateSystem<GridLodSystem>();
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                f.World.CompleteAll(); // Scheduling only; worker execution is outside this stated main-thread target.
            }
            var stats = PerfRun.Gate("GridLod5000", samples, .1);
            TestContext.WriteLine($"5000 bricks: scheduling median {stats.Median:F6} ms, p95 {stats.P95:F6} ms; initial tuning passes 0");
        }
    }
}
