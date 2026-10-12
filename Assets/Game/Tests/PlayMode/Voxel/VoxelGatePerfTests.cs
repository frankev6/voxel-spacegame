using System;
using System.Collections;
using System.Collections.Generic;
using Game.Tests.Common.Perf;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Mathematics;
using Game.Tests.PlayMode.Voxel.Physics.Perf;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Voxel
{
    public sealed class VoxelGatePerfTests
    {
        static void PositionCamera(BrickRenderFixture f)
        {
            // Reference station centred at (64,32,64), precisely 200 m from the camera.
            var center = new Vector3(64,32,64); f.Camera.transform.position = center + new Vector3(0,80,-Mathf.Sqrt(200*200-80*80)); f.Camera.transform.LookAt(center);
        }
        [UnityTest, Category("Performance")] public IEnumerator IdleStation1M_VoxelMainThreadUnder2ms()
        {
            using var scope = new PhysicsPerfScope(); using var f = new BrickRenderFixture("station"); PositionCamera(f); yield return f.Ready(true); f.ReleaseSource();
            using var recorder = new BrickRenderRecorder(f,false,true); double before = 0; var samples = new List<double>();
            f.BeforeFrame = () => before = recorder.CurrentMs; f.AfterFrame = () => samples.Add(recorder.CurrentMs-before);
            yield return f.WaitFrames(60); samples.Clear(); yield return f.WaitFrames(300);
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("VoxelIdle",samples); run.Write("VoxelIdle",2);
            TestContext.WriteLine($"All voxel systems + Entities Graphics, 872 meshed/collidable bricks: {run.Get("VoxelIdle").Median:F6} / {run.Get("VoxelIdle").P95:F6} ms median/p95");
            Assert.Greater(run.Get("VoxelIdle").Median,0); run.AssertMedian("VoxelIdle",2);
        }
        [UnityTest, Category("Performance")] public IEnumerator HollowStation1M_MemoryUnder40MB() => Memory("station",true);
        [UnityTest, Category("Performance")] public IEnumerator ThinShell_Report() => Memory("thin",false);
        static IEnumerator Memory(string shape, bool assertBudget)
        {
            using var scope = new PhysicsPerfScope(); using var f = new BrickRenderFixture(shape); if (shape == "station") PositionCamera(f);
            yield return f.Ready(true); f.ReleaseSource(); Assert.AreEqual(0,f.SourceBytes,"Source evicted after completed spawn per SPEC-10 §8.2");
            yield return f.WaitFrames(60); var samples = new List<double>(); VoxelMemoryStats peak = default;
            int last = f.Frames;
            for (int i=0;i<300;i++)
            {
                // Four consecutive damage buckets retain old/new collider and mesh versions together (SPEC-10 §8.2 / ADR-0033).
                if (i < 16 && i % 4 == 0) DamagePatch(f,i/4,shape == "thin" ? 1 : 4);
                yield return f.WaitFrames(1); Assert.Greater(f.Frames,last); last = f.Frames;
                var stats = VoxelMemoryStats.Capture(f); samples.Add(stats.Total); if (stats.Total > peak.Total) peak = stats;
            }
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("VoxelBytes",samples,"bytes"); run.Write("VoxelBytes",assertBudget ? 40000000 : double.NaN);
            TestContext.WriteLine(shape + ": " + peak + "; source=0; 60 warm-up + 300 samples; shared content/engine/driver heaps excluded, capacities include unused pool space");
            Assert.Greater(peak.BufferHeap,0); Assert.Greater(peak.MeshGpu,0); Assert.Greater(peak.ColliderBlobs,0);
            if (assertBudget)
            {
                run.AssertMax("VoxelBytes",40000000);
                Assert.Inconclusive("Accounted voxel-owned capacity is within 40 MB, but system-private scratch and engine allocation overhead are not included; total peak is unavailable. See SPEC-71 Q10.");
            }
        }
        static void DamagePatch(BrickRenderFixture f, int patch, int depth)
        {
            var grid = f.Em.GetComponentData<VoxelGrid>(f.Root);
            for (int z=8;z<12;z++) for (int y=8+patch*8;y<12+patch*8;y++) for (int x=0;x<depth;x++)
            {
                var cell = new int3(x,y,z); var brickEntity = f.Em.GetBuffer<BrickSlot>(f.Root)[BrickMath.SlotIndex(BrickMath.BrickOf(cell),grid.BrickMin,grid.BrickDims)].Brick;
                var brick = f.Em.GetComponentData<Brick>(brickEntity); var dirty = f.Em.GetComponentData<BrickDirty>(brickEntity);
                Assert.IsTrue(BrickWrite.Set(ref brick,f.Em.GetBuffer<BrickCells>(brickEntity),f.Em.GetBuffer<BrickSparseCells>(brickEntity),f.Em.GetBuffer<BrickSolidMask>(brickEntity),ref dirty,
                    BrickMath.LocalIndex(BrickMath.LocalOf(cell)),default,grid.Revision+1));
                f.Em.SetComponentData(brickEntity,brick); f.Em.SetComponentData(brickEntity,dirty); f.Em.SetComponentEnabled<BrickDirty>(brickEntity,true);
            }
            grid.Revision++; f.Em.SetComponentData(f.Root,grid); f.Em.SetComponentEnabled<GridMassRecompute>(f.Root,true);
        }
        [UnityTest, Category("Performance")] public IEnumerator SpawnStation1M_NoFrameOver8ms_ReadyWithin30Frames()
        {
            using var scope = new PhysicsPerfScope();
            // Warm the actual systems/shaders; the measured world below still starts with empty pools.
            using (var warm = new BrickRenderFixture("station")) { PositionCamera(warm); yield return warm.Ready(true); yield return warm.WaitFrames(60); }
            using var f = new BrickRenderFixture("station"); PositionCamera(f);
            // System marker names are registered during world construction; discover them before its first camera update.
            using var recorder = new BrickRenderRecorder(f,false,true); double before = 0; var samples = new List<double>(); int readyAt = 0;
            f.BeforeFrame = () => before = recorder.CurrentMs;
            f.AfterFrame = () => { samples.Add(recorder.CurrentMs-before); if (readyAt == 0 && f.IsReady(true)) readyAt = f.Frames; };
            yield return f.WaitFrames(300);
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("VoxelSpawnMain",samples).AddSamples("ReadyFrames",new[] { (double)readyAt },"frames");
            run.Write("VoxelSpawnMain",budgetP95:8);
            TestContext.WriteLine($"Station full mesh + valid installed collider ready frame {readyAt}; voxel main median/p95/max {run.Get("VoxelSpawnMain").Median:F6}/{run.Get("VoxelSpawnMain").P95:F6}/{run.Get("VoxelSpawnMain").Max:F6} ms; 300 measured frames");
            Assert.Greater(readyAt,0,"Station never became meshed and collidable"); Assert.Greater(run.Get("VoxelSpawnMain").Max,0);
            Assert.IsTrue(run.Get("VoxelSpawnMain").Max <= 8 && readyAt <= 30, $"Advisory spawn targets: max {run.Get("VoxelSpawnMain").Max:F6} ms <= 8; ready {readyAt} frames <= 30");
        }
    }
}
