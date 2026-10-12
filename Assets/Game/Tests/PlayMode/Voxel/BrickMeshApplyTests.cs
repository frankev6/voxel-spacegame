using System;
using System.Collections;
using System.Diagnostics;
using Game.Data.Services;
using Game.Tests.Common.Perf;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Voxel
{
    /// <summary>REN-011 real GPU registration, pooled rebuilds, retirement, bounds and advisory apply cost.</summary>
    public sealed class BrickMeshApplyTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly World World;
            readonly ContentService content;
            readonly SystemHandle apply, cleanup, disposal, render;
            readonly BeginInitializationEntityCommandBufferSystem begin;
            readonly VoxelRenderConfig config;
            public readonly Entity State;
            public readonly Camera Camera;
            public readonly Entity[] Bricks;
            public EntityManager EM => World.EntityManager;
            public BrickPoolRenderer Pool => EM.GetComponentData<VoxelRenderRuntime>(State).Managed.Value.Pool;
            public Fixture(int count = 1)
            {
                content = new ContentService(Resources.Load<Game.App.Boot.GameRoot>("GameRoot").Config.ContentDatabase);
                World = new World("BrickMeshApplyTests");
                World.GetOrCreateSystemManaged<PresentationSystemGroup>();
                World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
                begin = World.GetOrCreateSystemManaged<BeginInitializationEntityCommandBufferSystem>();
                apply = World.CreateSystem<BrickMeshApplySystem>(); cleanup = World.CreateSystem<BrickRenderCleanupSystem>();
                disposal = World.CreateSystem<DeferredDisposalSystem>(); render = World.CreateSystem<BrickPoolRenderSystem>();
                State = VoxelSingletons.CreateAll(EM); content.CreateSingletons(EM);
                config = VoxelRenderConfigFactory.Create(content.Catalog.Get<Game.Data.Definitions.VoxelRenderConfigDefinition>(new Game.Core.ContentId("config_voxel_render")), content.PropMeshes);
                EM.CreateSingleton(config); apply.Update(World.Unmanaged);
                Assert.AreEqual(1, EM.GetComponentData<VoxelRenderRuntime>(State).IsReady);
                Camera = new GameObject("Apply test camera").AddComponent<Camera>(); Camera.enabled = false;
                Camera.transform.position = new Vector3(0, 0, -10); Camera.transform.rotation = Quaternion.identity;
                EM.SetComponentData(State, new VoxelRenderCamera { Value = Camera });
                Bricks = new Entity[count];
                for (int i = 0; i < count; i++)
                {
                    var brick = EM.CreateEntity(typeof(Brick), typeof(BrickRenderState), typeof(BrickPropInstance), typeof(LocalToWorld)); Bricks[i] = brick;
                    EM.SetComponentData(brick, new LocalToWorld { Value = float4x4.Translate(new float3(i * 2, 0, 0)) });
                }
            }
            public void Build(bool empty = false, int offset = 0)
            {
                var batch = EM.GetComponentData<BrickMeshBatch>(State); batch.Work.Clear();
                for (int i = 0; i < Bricks.Length; i++)
                {
                    var snapshot = batch.Snapshots.GetSubArray(i * MeshCell.SnapCells, MeshCell.SnapCells);
                    for (int j = 0; j < snapshot.Length; j++) snapshot[j] = 0;
                    if (!empty) snapshot[MeshCell.SnapIndex(offset, 0, 0)] = MeshCell.Encode(1, 0, 0, 0, 0, 0, false);
                    batch.Work.Add(new BrickMeshWork { Brick = Bricks[i], PropStart = i * 512, PropCount = 1 });
                    batch.Props[i * 512] = new BrickPropInstance { Block = 1, LocalIndex = (ushort)offset, Color = 7 };
                }
                batch.Handle = new BrickMeshJob { Work = batch.Work.AsArray(), Results = batch.Results, Snapshots = batch.Snapshots,
                    Shapes = EM.GetComponentData<VoxelRenderRuntime>(State).Shapes }.Schedule(Bricks.Length, 1);
                batch.InFlight = 1; batch.ScheduledFrame = EM.GetComponentData<DeferredDisposalQueue>(State).Frame;
                EM.SetComponentData(State, batch); batch.Handle.Complete();
            }
            public void Apply() => apply.Update(World.Unmanaged);
            public void Frame()
            { begin.Update(); cleanup.Update(World.Unmanaged); apply.Update(World.Unmanaged); disposal.Update(World.Unmanaged); }
            public BrickRenderState StateOf(int index = 0) => EM.GetComponentData<BrickRenderState>(Bricks[index]);
            public void Draw() => render.Update(World.Unmanaged);
            public void Dispose()
            {
                VoxelSingletons.DisposeAll(EM, State); World.Dispose();
                var owned = config; VoxelRenderConfigFactory.Destroy(ref owned); content.Dispose();
                Object.DestroyImmediate(Camera.gameObject);
            }
        }
        [SetUp] public void RequireGpu()
        { if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Real GPU required for pooled render application; run the GPU PlayMode suite."); }
        [UnityTest] public IEnumerator RegisterOnce_RewriteInPlace()
        {
            using var f = new Fixture(); f.Build(); f.Apply(); var first = f.StateOf();
            for (int i = 0; i < 20; i++)
            {
                f.Build(offset: i % 8); f.Apply(); var state = f.StateOf();
                Assert.AreEqual(first.Handle, state.Handle); Assert.AreEqual(first.Vertices, state.Vertices); Assert.AreEqual(first.Indices, state.Indices);
                Assert.AreEqual(1, f.Pool.LiveCount); Assert.AreEqual(i % 8, f.EM.GetBuffer<BrickPropInstance>(f.Bricks[0])[0].LocalIndex);
            }
            Assert.AreEqual(1, BrickRenderStats.Gather(f.EM).Meshes);
            Assert.AreEqual(0, BrickRenderStats.Gather(f.EM).CpuCopyBytes);
            yield return null;
        }
        [UnityTest] public IEnumerator DestroyedBrick_MeshRetired()
        {
            using var f = new Fixture(); f.Build(); f.Apply();
            f.Build(); f.EM.DestroyEntity(f.Bricks[0]); f.Frame();
            Assert.AreEqual(0, f.Pool.LiveCount); Assert.AreEqual(1, f.Pool.RetiredRangeCount);
            f.Frame(); Assert.IsFalse(f.EM.Exists(f.Bricks[0])); Assert.AreEqual(1, f.Pool.RetiredRangeCount);
            f.Frame(); Assert.AreEqual(1, f.Pool.RetiredRangeCount);
            f.Frame(); Assert.AreEqual(0, f.Pool.RetiredRangeCount);
            var batch = f.EM.GetComponentData<BrickMeshBatch>(f.State);
            Assert.AreEqual(0, batch.InFlight); Assert.IsFalse(batch.Results[0].Vertices.IsCreated);
            yield return null;
        }
        [UnityTest] public IEnumerator EmptyMesh_Hidden()
        {
            using var f = new Fixture(); f.Build(); f.Apply(); var handle = f.StateOf().Handle;
            f.Build(empty: true); f.Apply(); f.Draw();
            Assert.AreEqual(handle, f.StateOf().Handle); Assert.AreEqual(1, f.StateOf().HasMesh);
            Assert.AreEqual(0, f.StateOf().VertexCount); Assert.AreEqual(0, f.Pool.VisibleCount); Assert.AreEqual(0, f.Pool.DrawCount);
            yield return null;
        }
        [UnityTest] public IEnumerator Bounds_Updated()
        {
            using var f = new Fixture(); f.Build(offset: 3); f.Apply();
            var bounds = f.Pool.GetBounds(f.StateOf().Handle);
            Assert.AreEqual(new float3(3.5f, .5f, .5f), bounds.Center); Assert.AreEqual(new float3(.5f), bounds.Extents);
            var transform = float4x4.TRS(new float3(5, 0, 0), quaternion.RotateY(.7f), new float3(.5f));
            f.EM.SetComponentData(f.Bricks[0], new LocalToWorld { Value = transform }); f.Draw();
            Assert.AreEqual(transform, f.Pool.WorldMatrices[f.StateOf().Handle.Slot]);
            yield return null;
        }
        [UnityTest, Category("Performance")] public IEnumerator ApplyBudget128()
        {
            using var f = new Fixture(128); var samples = new double[30];
            for (int i = -5; i < samples.Length; i++)
            {
                f.Build(); long start = Stopwatch.GetTimestamp(); f.Apply();
                double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                if (i >= 0) samples[i] = ms;
                f.Frame(); yield return null;
            }
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("Apply128", samples, "ms");
            run.Write("Apply128", 2.5); run.AssertMedian("Apply128", 2.5);
        }
        [UnityTest, Category("Performance")] public IEnumerator NoGcSteadyState()
        {
            using var f = new Fixture();
            for (int i = 0; i < 10; i++) { f.Build(); f.Apply(); yield return null; }
            long total = 0;
            for (int i = 0; i < 30; i++)
            {
                f.Build(); long before = GC.GetAllocatedBytesForCurrentThread(); f.Apply();
                total += GC.GetAllocatedBytesForCurrentThread() - before; yield return null;
            }
            Assert.AreEqual(0, total, "Apply update after first-build warmup");
        }
    }
}
