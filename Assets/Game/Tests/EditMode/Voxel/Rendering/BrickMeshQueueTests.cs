using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    /// <summary>VOX-021 (extracted from REN-010): priority/FIFO, deduplication and guarded job access.</summary>
    public sealed partial class BrickMeshQueueTests
    {
        static Entity Brick(int index) => new Entity { Index = index, Version = 1 };
        [Test]
        public void PriorityOrder()
        {
            var queue = BrickMeshQueue.Create();
            try
            {
                for (int priority = 3; priority >= 0; priority--)
                    for (int i = 0; i < 3; i++) queue.Enqueue(Brick(priority * 3 + i + 1), Brick(100), (MeshPriority)priority);
                Assert.AreEqual(12, queue.Count);
                for (int priority = 0; priority < 4; priority++) for (int i = 0; i < 3; i++)
                {
                    Assert.IsTrue(queue.TryDequeue(out var request)); Assert.AreEqual((MeshPriority)priority, request.Priority);
                    Assert.AreEqual(Brick(priority * 3 + i + 1), request.Brick); Assert.AreEqual(Brick(100), request.Grid);
                }
                Assert.IsFalse(queue.TryDequeue(out _)); Assert.AreEqual(0, queue.Count);
                foreach (int head in queue.Heads) Assert.AreEqual(0, head);
                Assert.AreEqual(0, queue.Damage.Length + queue.Neighbour.Length + queue.Spawn.Length + queue.Lod.Length);
            }
            finally { queue.Dispose(); }
        }
        [Test]
        public void UpgradeDedup()
        {
            var queue = BrickMeshQueue.Create();
            try
            {
                queue.Enqueue(Brick(1), Brick(100), MeshPriority.Spawn);
                queue.Enqueue(Brick(1), Brick(100), MeshPriority.Lod);
                queue.Enqueue(Brick(1), Brick(100), MeshPriority.Spawn);
                Assert.AreEqual(1, queue.Spawn.Length); Assert.AreEqual(0, queue.Lod.Length);
                queue.Enqueue(Brick(1), Brick(100), MeshPriority.Damage);
                queue.Enqueue(Brick(1), Brick(100), MeshPriority.Neighbour);
                Assert.AreEqual(1, queue.Count); Assert.AreEqual(0, queue.Neighbour.Length);
                Assert.IsTrue(queue.TryDequeue(out var request)); Assert.AreEqual(MeshPriority.Damage, request.Priority);
                Assert.IsFalse(queue.TryDequeue(out _)); Assert.AreEqual(0, queue.Count);
            }
            finally { queue.Dispose(); }
        }
        [Test]
        public void StaleSkipped()
        {
            var queue = BrickMeshQueue.Create();
            try
            {
                for (int i = 0; i < 2048; i++)
                {
                    queue.Enqueue(Brick(i + 1), Brick(3000), MeshPriority.Lod);
                    queue.Enqueue(Brick(i + 1), Brick(3000), MeshPriority.Spawn);
                    queue.Enqueue(Brick(i + 1), Brick(3000), MeshPriority.Neighbour);
                    queue.Enqueue(Brick(i + 1), Brick(3000), MeshPriority.Damage);
                }
                Assert.AreEqual(2048, queue.Count);
                for (int i = 0; i < 2048; i++)
                {
                    Assert.IsTrue(queue.TryDequeue(out var request)); Assert.AreEqual(Brick(i + 1), request.Brick);
                    Assert.AreEqual(MeshPriority.Damage, request.Priority);
                }
                Assert.IsFalse(queue.TryDequeue(out _)); Assert.AreEqual(0, queue.Count);
                // Requeue after a promoted request was consumed, before its old low-priority entry is drained.
                queue.Enqueue(Brick(1), Brick(3000), MeshPriority.Lod);
                queue.Enqueue(Brick(2), Brick(3000), MeshPriority.Lod);
                queue.Enqueue(Brick(1), Brick(3000), MeshPriority.Damage);
                Assert.IsTrue(queue.TryDequeue(out var promoted)); Assert.AreEqual(Brick(1), promoted.Brick);
                queue.Enqueue(Brick(1), Brick(4000), MeshPriority.Lod);
                Assert.IsTrue(queue.TryDequeue(out var older)); Assert.AreEqual(Brick(2), older.Brick);
                Assert.IsTrue(queue.TryDequeue(out var requeued)); Assert.AreEqual(Brick(1), requeued.Brick); Assert.AreEqual(Brick(4000), requeued.Grid);
                Assert.IsFalse(queue.TryDequeue(out _));
                queue.Enqueue(Brick(1), Brick(3000), MeshPriority.Spawn);
                Assert.IsTrue(queue.TryDequeue(out var fresh)); Assert.AreEqual(MeshPriority.Spawn, fresh.Priority);
                Assert.IsFalse(queue.TryDequeue(out _));
            }
            finally { queue.Dispose(); }
        }
        [BurstCompile(CompileSynchronously = true)]
        struct ProducerJob : IJob
        {
            public NativeList<BrickMeshRequest> Damage, Neighbour, Spawn, Lod;
            public NativeArray<int> Heads;
            public NativeHashMap<Entity, byte> Pending;
            public void Execute()
            {
                // The local view borrows only containers declared on this job. Access is never a job field.
                var queue = new BrickMeshQueue { Damage = Damage, Neighbour = Neighbour, Spawn = Spawn, Lod = Lod, Heads = Heads, Pending = Pending };
                for (int i = 0; i < 1024; i++) queue.Enqueue(new Entity { Index = i + 1, Version = 1 }, new Entity { Index = 2000, Version = 1 }, MeshPriority.Spawn);
            }
        }
        [DisableAutoCreation, BurstCompile]
        [UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
        partial struct ProducerSystem : ISystem
        {
            public void OnCreate(ref SystemState state) { state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<BrickMeshQueue>(); }
            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                var queue = SystemAPI.GetSingleton<BrickMeshQueue>();
                var job = new ProducerJob { Damage = queue.Damage, Neighbour = queue.Neighbour, Spawn = queue.Spawn, Lod = queue.Lod, Heads = queue.Heads, Pending = queue.Pending };
                state.Dependency = job.Schedule(queue.Acquire(state.Dependency)); queue.Release(state.Dependency);
            }
        }
        [Test]
        public void JobProducer_MainThreadConsumer_NoSafetyErrors()
        {
            bool safety = BurstCompiler.Options.EnableBurstSafetyChecks, compilation = BurstCompiler.Options.EnableBurstCompilation;
            var queue = BrickMeshQueue.Create();
            try
            {
                BurstCompiler.Options.EnableBurstSafetyChecks = true; BurstCompiler.Options.EnableBurstCompilation = true;
                using var world = new World("BrickMeshQueueTests");
                world.EntityManager.CreateEntity(typeof(GameplayActive)); world.EntityManager.CreateSingleton(queue);
                var system = world.CreateSystem<ProducerSystem>();
                system.Update(world.Unmanaged);
                queue.CompleteAccess();
                Assert.AreEqual(1024, queue.Count);
                for (int i = 0; i < 1024; i++)
                { Assert.IsTrue(queue.TryDequeue(out var request)); Assert.AreEqual(Brick(i + 1), request.Brick); }
                Assert.IsFalse(queue.TryDequeue(out _));
            }
            finally { queue.Dispose(); BurstCompiler.Options.EnableBurstSafetyChecks = safety; BurstCompiler.Options.EnableBurstCompilation = compilation; }
        }
    }
}
