using System.Diagnostics;
using System.Threading;
using Game.Core;
using Game.Core.Components;
using Game.Tests.Common;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel.Physics
{
    /// <summary>PHY-023: unfinished detached batches meet urgent deadlines outside fixed-step work (ADR-0035).</summary>
    public sealed class ColliderDeadlineTests : GameTestBase
    {
        EcsTestWorld test;
        EntityManager em;
        Entity clock, frame, root;
        ColliderDirtyQueue queue;
        ColliderBuildCache cache;
        ColliderBatchQueue batch;
        SystemHandle builder, swap;
        [SetUp] public void SetUp()
        {
            test = CreateWorld(typeof(BrickColliderBuildSystem), typeof(ColliderSwapSystem)); em = test.EntityManager;
            test.AddGameplayActive(); clock = test.AddSimClock();
            queue = ColliderDirtyQueue.Create(); cache = ColliderBuildCache.Create(); batch = ColliderBatchQueue.Create();
            em.CreateSingleton(queue); em.CreateSingleton(cache); em.CreateSingleton(batch);
            em.CreateSingleton(PhysicsVoxelConfig.Default); frame = em.CreateSingleton(new DeferredDisposalQueue { Frame = 10 });
            builder = test.World.GetExistingSystem<BrickColliderBuildSystem>(); swap = test.World.GetExistingSystem<ColliderSwapSystem>();
            // Warm dispatch before scheduling a deliberately unfinished batch.
            builder.Update(test.World.Unmanaged);
            root = em.CreateEntity(typeof(PhysicsCollider), typeof(GridColliderState)); var original = Box(0);
            em.SetComponentData(root, new PhysicsCollider { Value = original });
            ColliderPipeline.AddRecord(ref cache.C, root, 1, .5f, GridMotion.Dynamic, installed: original);
        }
        [TearDown] public void TearDown()
        { test.CompleteAll(); cache.CompleteAccess(); batch.Dispose(); cache.Dispose(); queue.Dispose(); }
        static BlobAssetReference<Collider> Box(float x) => BoxCollider.Create(new BoxGeometry
            { Center = new float3(x, 0, 0), Size = new float3(1), Orientation = quaternion.identity }, PhysicsLayers.Grid, PhysicsLayers.GridMaterial);
        struct DelayedBatch : IJob { public void Execute() => Thread.Sleep(500); }
        int StartBatch(ColliderPriority priority, uint due)
        {
            int index = ColliderPipeline.GetOrCreateUpdate(ref cache.C, 1, due, 0, priority);
            ColliderPipeline.Seal(ref cache.C, index); var update = cache.C.Updates[index]; update.State = UpdateState.Building;
            cache.C.Updates[index] = update;
            batch.B.Assemblies.Add(new AssemblyItem { Update = index, Serial = 1 });
            batch.B.AssemblyResults.Add(new AssemblyResult { Compound = Box(2), ChildCount = 1, BoxCount = 1 });
            batch.State.Value = new ColliderBatchState { InFlight = 1, Handle = new DelayedBatch().Schedule(), ScheduledFrame = 10 };
            JobHandle.ScheduleBatchedJobs(); Assert.IsFalse(batch.State.Value.Handle.IsCompleted);
            return index;
        }
        [Test] public void Urgent_ReadyBeforeCatchUp()
        {
            int index = StartBatch(ColliderPriority.Damage, 8);
            em.SetComponentData(clock, new SimClock { Tick = 3 });
            var watch = Stopwatch.StartNew(); builder.Update(test.World.Unmanaged); watch.Stop();
            Assert.AreEqual(0, batch.State.Value.InFlight); Assert.AreEqual(UpdateState.Built, cache.C.Updates[index].State);
            Assert.AreEqual(1, cache.C.Stats.Value.ForcedBatchCompletes);
            var original = em.GetComponentData<PhysicsCollider>(root).Value;
            for (uint tick = 3; tick < 3 + SimTime.MaxTicksPerFrame; tick++)
            {
                em.SetComponentData(clock, new SimClock { Tick = tick }); swap.Update(test.World.Unmanaged); cache.CompleteAccess();
                if (tick < 8) Assert.AreEqual(original, em.GetComponentData<PhysicsCollider>(root).Value);
                else Assert.AreEqual(8, em.GetComponentData<GridColliderState>(root).InstalledTick);
            }
            Assert.AreEqual(0, cache.C.Stats.Value.LateInstalls); ColliderPipelineInvariants.Check(cache, batch);
            TestContext.WriteLine($"Deliberate 500 ms batch stall completed outside the fixed step in {watch.Elapsed.TotalMilliseconds:F3} ms; install tick 8, zero late installs.");
        }
        [Test] public void NonUrgent_PreservesAgeLimit()
        {
            int index = StartBatch(ColliderPriority.Spawn, 0);
            builder.Update(test.World.Unmanaged);
            Assert.AreEqual(1, batch.State.Value.InFlight); Assert.IsFalse(batch.State.Value.Handle.IsCompleted);
            Assert.AreEqual(0, cache.C.Stats.Value.ForcedBatchCompletes);
            em.SetComponentData(frame, new DeferredDisposalQueue { Frame = 12 }); builder.Update(test.World.Unmanaged);
            Assert.AreEqual(0, batch.State.Value.InFlight); Assert.AreEqual(UpdateState.Built, cache.C.Updates[index].State);
            Assert.AreEqual(1, cache.C.Stats.Value.ForcedBatchCompletes); ColliderPipelineInvariants.Check(cache, batch);
        }
    }
}
