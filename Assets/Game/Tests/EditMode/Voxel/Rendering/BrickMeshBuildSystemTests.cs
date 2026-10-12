using System;
using System.Diagnostics;
using Game.Core;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    /// <summary>REN-010 single-flight ownership, adaptive sizing, stale requests and the advisory snapshot cost.</summary>
    public sealed class BrickMeshBuildSystemTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly EcsTestWorld World;
            readonly ContentService content;
            public readonly Entity State, Grid;
            public readonly Entity[] Bricks;
            public EntityManager EM => World.EntityManager;
            public Fixture(int count, int max = 128)
            {
                content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
                World = EcsTestWorld.Create(typeof(BrickMeshBuildSystem)); World.AddGameplayActive(); content.CreateSingletons(EM);
                State = EM.CreateEntity(typeof(BrickMeshQueue), typeof(BrickMeshBatch), typeof(VoxelRenderRuntime), typeof(VoxelRenderConfig), typeof(DeferredDisposalQueue));
                EM.SetComponentData(State, BrickMeshQueue.Create()); EM.SetComponentData(State, BrickMeshBatch.Create(16));
                EM.SetComponentData(State, new VoxelRenderRuntime { IsReady = 1, Shapes = BrickShapeTablesBuilder.Build(ref content.Orientations.Value) });
                EM.SetComponentData(State, new VoxelRenderConfig { MeshBatchMin = 16, MeshBatchMax = max, MeshApplyBudgetMs = 2 });
                Grid = EM.CreateEntity(typeof(VoxelGrid), typeof(PaletteEntry), typeof(BlockInstance), typeof(BrickSlot), typeof(Dormant));
                EM.SetComponentEnabled<Dormant>(Grid, false);
                EM.SetComponentData(Grid, new VoxelGrid { BrickDims = new int3(count, 1, 1) });
                var palette = EM.GetBuffer<PaletteEntry>(Grid); palette.Add(default);
                palette.Add(new PaletteEntry { Block = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_hull"))) });
                EM.GetBuffer<BrickSlot>(Grid).Resize(count, NativeArrayOptions.ClearMemory); Bricks = new Entity[count];
                for (int i = 0; i < count; i++)
                {
                    var brick = EM.CreateEntity(typeof(Brick), typeof(BrickCells), typeof(BrickSparseCells), typeof(BrickLodDesired)); Bricks[i] = brick;
                    EM.SetComponentData(brick, new Brick { Grid = Grid, Coord = new int3(i, 0, 0), SolidCount = 1 });
                    EM.GetBuffer<BrickSparseCells>(brick).Add(new BrickSparseCells { Local = 0, Value = Cell.Palette(1) });
                    var slots = EM.GetBuffer<BrickSlot>(Grid); slots[i] = new BrickSlot { Brick = brick };
                }
                Enqueue();
            }
            public BrickMeshBatch Batch => EM.GetComponentData<BrickMeshBatch>(State);
            public BrickMeshQueue Queue => EM.GetComponentData<BrickMeshQueue>(State);
            public void Enqueue() { var q = Queue; q.CompleteAccess(); foreach (var brick in Bricks) q.Enqueue(brick, Grid, MeshPriority.Spawn); }
            public void ClearBatch(float average)
            {
                var batch = Batch; batch.ReleaseResults(); batch.InFlight = 0; batch.AvgApplyMicros = average; EM.SetComponentData(State, batch);
            }
            public void Dispose()
            {
                Batch.Dispose(); Queue.Dispose(); EM.GetComponentData<VoxelRenderRuntime>(State).Dispose();
                EM.DestroyEntity(State); World.Dispose(); content.Dispose();
            }
        }
        [Test] public void OneBatchInFlight()
        {
            using var f = new Fixture(48, 32); f.World.StepFrame(0);
            Assert.AreEqual(1, f.Batch.InFlight); Assert.AreEqual(16, f.Queue.Count);
            var batch = f.Batch; batch.Handle.Complete(); var first = batch.Work[0].Brick;
            f.Queue.Enqueue(first, f.Grid, MeshPriority.Damage);
            // A completed job still owns its output until Apply explicitly consumes it.
            f.World.StepFrame(0); Assert.AreEqual(32, f.Batch.Work.Length); Assert.AreEqual(17, f.Queue.Count);
            Assert.AreEqual(first, f.Batch.Work[0].Brick); Assert.Greater(batch.Results[0].Vertices.Length, 0);
            foreach (var field in typeof(BrickMeshJob).GetFields())
                Assert.IsFalse(field.FieldType.Name.Contains("Lookup"), "Cross-frame mesh work must contain no ECS lookup");
            f.ClearBatch(15); f.World.StepFrame(0); f.Batch.Handle.Complete();
            Assert.AreEqual(first, f.Batch.Work[0].Brick, "Re-dirtied work retains queue priority");
        }
        [Test] public void AdaptiveBatchSize()
        {
            foreach (var sample in new[] { (15f, 128), (1000f, 16), (31.25f, 64) })
            {
                using var f = new Fixture(128); f.ClearBatch(sample.Item1); f.World.StepFrame(0); f.Batch.Handle.Complete();
                Assert.AreEqual(sample.Item2, f.Batch.Work.Length);
            }
            using var dormant = new Fixture(16); dormant.EM.SetComponentEnabled<Dormant>(dormant.Grid, true);
            dormant.World.StepFrame(0); dormant.Batch.Handle.Complete();
            foreach (var work in dormant.Batch.Work) Assert.AreEqual(1, work.Lod);
        }
        [Test] public void DestroyedBricks_Skipped()
        {
            using var f = new Fixture(16); f.EM.DestroyEntity(f.Bricks[0]); f.World.StepFrame(0); f.Batch.Handle.Complete();
            Assert.AreEqual(15, f.Batch.Work.Length); Assert.AreEqual(0, f.Queue.Count);
            foreach (var work in f.Batch.Work) Assert.AreNotEqual(f.Bricks[0], work.Brick);
            // Mesh work owns a snapshot, so deleting live cells after scheduling cannot invalidate it.
            f.EM.DestroyEntity(f.Bricks[1]); Assert.Greater(f.Batch.Results[0].Indices.Length, 0);
        }
        [Test, Performance, Category("Performance")] public void MainThreadCost()
        {
            using var f = new Fixture(128); var samples = new double[30];
            for (int i = -5; i < samples.Length; i++)
            {
                f.ClearBatch(15); f.Enqueue(); long start = Stopwatch.GetTimestamp(); f.World.StepFrame(0);
                double elapsed = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                f.Batch.Handle.Complete(); Assert.AreEqual(128, f.Batch.Work.Length);
                if (i >= 0) samples[i] = elapsed;
            }
            // Includes the whole test World update, conservatively enclosing dequeue/snapshot/scheduling.
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("MeshBuild128", samples, "ms");
            run.Write("MeshBuild128", .5); run.AssertMedian("MeshBuild128", .5);
        }
    }
}
