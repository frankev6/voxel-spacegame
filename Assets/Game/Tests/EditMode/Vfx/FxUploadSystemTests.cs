using System;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Presentation.Vfx;
using Game.Tests.Common;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests.EditMode.Vfx
{
    public sealed class FxUploadSystemTests
    {
        [Test] public void Unbound_DrainsAndDiscards()
        {
            using var world = EcsTestWorld.Create(typeof(FxEventQueueCreateSystem), typeof(FxUploadSystem));
            using var query = world.EntityManager.CreateEntityQuery(typeof(FxEventQueue)); var q = query.GetSingleton<FxEventQueue>();
            for (int i = 0; i < 6; i++) q.Queue.Enqueue(FxEvents.Explosion(float3.zero, 1, float3.zero));
            world.StepFrame(1f / 60); var system = world.World.GetExistingSystemManaged<FxUploadSystem>();
            Assert.AreEqual(0, q.Queue.Count); Assert.AreEqual(6, system.Stats.EventsDrained); Assert.AreEqual(6, system.Stats.EventsOverHardCap); Assert.IsFalse(system.Stats.Bound);
        }
        [Test] public void HardCap_ClearsRemainder()
        {
            GameLog.ResetOnceKeys();
            using var world = EcsTestWorld.Create(typeof(FxEventQueueCreateSystem), typeof(FxUploadSystem)); using var f = new FxUploadFixture();
            var system = world.World.GetExistingSystemManaged<FxUploadSystem>(); system.Bind(f);
            using var query = world.EntityManager.CreateEntityQuery(typeof(FxEventQueue)); var q = query.GetSingleton<FxEventQueue>();
            for (int i = 0; i < 100000; i++) q.Queue.Enqueue(new FxEvent { Kind = Game.Data.Definitions.FxKind.HitKinetic });
            LogAssert.Expect(LogType.Warning, new Regex("FX events exceeded the 65536")); world.StepFrame(1f / 60);
            Assert.AreEqual(0, q.Queue.Count); Assert.AreEqual(65536, f.Stats.EventsDrained); Assert.AreEqual(34464, f.Stats.EventsOverHardCap); Assert.AreEqual(64, f.Submitted);
            system.Bind(null);
        }
        [Test] public void MultipleTicksPerFrame_AllEventsSubmittedOnce()
        {
            using var world = EcsTestWorld.Create(typeof(FxEventQueueCreateSystem), typeof(FxUploadSystem), typeof(FxTickProducer));
            using var f = new FxUploadFixture(); var system = world.World.GetExistingSystemManaged<FxUploadSystem>(); system.Bind(f);
            world.StepFrame(3f / 60);
            Assert.AreEqual(3, f.Submitted); Assert.AreEqual(6, f.PositionSum); Assert.AreEqual(1, f.Stats.BatchesSubmitted);
            system.Update(); Assert.AreEqual(3, f.Submitted); Assert.AreEqual(0, f.Stats.BatchesSubmitted);
            var pending = f.PendingImpacts; pending.Add(new FxEvent { Kind = Game.Data.Definitions.FxKind.HitKinetic, Pos = new float3(4, 0, 0) });
            system.Update(); Assert.AreEqual(4, f.Submitted); Assert.AreEqual(10, f.PositionSum); Assert.AreEqual(0, pending.Length);
            using var query = world.EntityManager.CreateEntityQuery(typeof(FxEventQueue)); var queue = query.GetSingleton<FxEventQueue>().Queue;
            var item = new FxEvent { Kind = Game.Data.Definitions.FxKind.HitKinetic };
            for (int i = 0; i < 8; i++) { queue.Enqueue(item); system.Update(); }
            using var drain = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "FxUploadSystem.Drain");
            using var batch = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "FxUploadSystem.Batch");
            using var submit = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "FxUploadSystem.Submit");
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { queue.Enqueue(item); system.Update(); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "Warmed gameplay drain/batch/submit path");
            Assert.Greater(drain.CurrentValue, 0); Assert.Greater(batch.CurrentValue, 0); Assert.Greater(submit.CurrentValue, 0);
            Assert.AreEqual(112, f.Submitted); system.Bind(null);
        }
    }
    [DisableAutoCreation, UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct FxTickProducer : ISystem
    {
        int tick;
        public void OnUpdate(ref SystemState state)
        {
            var stream = SystemAPI.GetSingleton<FxEventQueue>(); stream.Access.CompleteAccess();
            stream.Queue.Enqueue(new FxEvent { Kind = Game.Data.Definitions.FxKind.HitKinetic, Pos = new float3(++tick, 0, 0) });
        }
    }
}
