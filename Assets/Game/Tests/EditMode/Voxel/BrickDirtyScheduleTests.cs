using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel
{
    [Category("Voxel")]
    public sealed class BrickDirtyScheduleTests : GameTestBase
    {
        struct AlternateArchetype : IComponentData { }
        EcsTestWorld test;
        EntityManager em;
        SystemHandle system;
        BrickMeshQueue mesh;
        ColliderDirtyQueue collider;
        Entity root;
        [SetUp] public void SetUp()
        {
            test = CreateWorld(typeof(BrickDirtyScheduleSystem)); em = test.EntityManager;
            test.AddGameplayActive(); system = test.World.GetExistingSystem<BrickDirtyScheduleSystem>();
            mesh = BrickMeshQueue.Create(); collider = ColliderDirtyQueue.Create();
            em.CreateSingleton(mesh); em.CreateSingleton(collider);
        }
        [TearDown] public void TearDown()
        {
            test.CompleteAll(); mesh.Dispose(); collider.Dispose();
        }
        Entity[] Bricks(int count, bool varied = false)
        {
            root = em.CreateEntity(typeof(VoxelGrid), typeof(BrickSlot));
            em.SetComponentData(root, new VoxelGrid { Serial = 77, BrickMin = int3.zero, BrickDims = new int3(count, 1, 1) });
            var entities = new Entity[count];
            for (int i = 0; i < count; i++)
            {
                Entity brick = em.CreateEntity(typeof(Brick), typeof(BrickDirty));
                if (varied && i % 2 == 0) em.AddComponent<AlternateArchetype>(brick);
                em.SetComponentData(brick, new Brick { Grid = root, Coord = new int3(i, 0, 0), SolidCount = 1 });
                em.SetComponentEnabled<BrickDirty>(brick, false); entities[i] = brick;
            }
            var slots = em.GetBuffer<BrickSlot>(root); slots.ResizeUninitialized(count);
            for (int i = 0; i < count; i++) slots[i] = new BrickSlot { Brick = entities[i] };
            return entities;
        }
        void Dirty(Entity entity, BlockFaceMask border = BlockFaceMask.PosX)
        {
            em.SetComponentData(entity, new BrickDirty { Flags = BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider, BorderMask = border });
            em.SetComponentEnabled<BrickDirty>(entity, true);
        }
        void Tick()
        {
            system.Update(test.World.Unmanaged); mesh.CompleteAccess(); collider.CompleteAccess(); test.CompleteAll();
        }
        void Clear()
        {
            while (mesh.TryDequeue(out _)) { }
            collider.Requests.Clear();
        }
        [Test] public void Drain_EnqueuesExpected()
        {
            var bricks = Bricks(2); Dirty(bricks[0]); Tick();
            Assert.AreEqual(1, collider.Requests.Length); var request = collider.Requests[0];
            Assert.AreEqual(root, request.Grid); Assert.AreEqual(bricks[0], request.Brick); Assert.AreEqual(77, request.Serial);
            Assert.AreEqual(int3.zero, request.Coord); Assert.AreEqual(ColliderPriority.Damage, request.Priority);
            Assert.IsTrue(mesh.TryDequeue(out var own)); Assert.AreEqual(bricks[0], own.Brick); Assert.AreEqual(MeshPriority.Damage, own.Priority);
            Assert.IsTrue(mesh.TryDequeue(out var neighbour)); Assert.AreEqual(bricks[1], neighbour.Brick); Assert.AreEqual(MeshPriority.Neighbour, neighbour.Priority);
            Assert.IsFalse(mesh.TryDequeue(out _)); Assert.IsFalse(em.IsComponentEnabled<BrickDirty>(bricks[0]));
            Assert.AreEqual(default(BrickDirty), em.GetComponentData<BrickDirty>(bricks[0]));
        }
        [Test] public void EmptyBrick_SkipsOwnMesh()
        {
            var bricks = Bricks(2); var empty = em.GetComponentData<Brick>(bricks[0]); empty.SolidCount = 0; em.SetComponentData(bricks[0], empty);
            Dirty(bricks[0], BlockFaceMask.All); Tick();
            Assert.AreEqual(1, collider.Requests.Length); Assert.AreEqual(bricks[0], collider.Requests[0].Brick);
            Assert.IsTrue(mesh.TryDequeue(out var neighbour)); Assert.AreEqual(bricks[1], neighbour.Brick); Assert.AreEqual(MeshPriority.Neighbour, neighbour.Priority);
            Assert.IsFalse(mesh.TryDequeue(out _)); Assert.IsFalse(em.IsComponentEnabled<BrickDirty>(bricks[0]));
        }
        [Test] public void Order_IsDeterministic()
        {
            var bricks = Bricks(1024, true); int previous = JobsUtility.JobWorkerCount;
            ColliderRequest[] expectedCollider = null; BrickMeshRequest[] expectedMesh = null;
            try
            {
                foreach (int workers in new[] { 1, JobsUtility.JobWorkerMaximumCount })
                {
                    JobsUtility.JobWorkerCount = workers;
                    for (int repeat = 0; repeat < 10; repeat++)
                    {
                        Clear();
                        for (int i = 0; i < bricks.Length; i++) if (i % 3 != 0) Dirty(bricks[i], BlockFaceMask.PosX | BlockFaceMask.NegX);
                        Tick(); var actualCollider = collider.Requests.AsArray().ToArray(); var actualMesh = new List<BrickMeshRequest>();
                        while (mesh.TryDequeue(out var request)) actualMesh.Add(request);
                        if (expectedCollider == null) { expectedCollider = actualCollider; expectedMesh = actualMesh.ToArray(); }
                        else { CollectionAssert.AreEqual(expectedCollider, actualCollider); CollectionAssert.AreEqual(expectedMesh, actualMesh); }
                    }
                }
                Assert.AreEqual(682, expectedCollider.Length); Assert.AreEqual(1024, expectedMesh.Length);
                TestContext.WriteLine($"Identical queue order across 10 runs each with 1 and {JobsUtility.JobWorkerMaximumCount} workers; 682 collider / 1024 mesh requests.");
            }
            finally { JobsUtility.JobWorkerCount = previous; }
        }
        [Test] public void NoDirty_EarlyOut()
        {
            Tick(); Assert.AreEqual(default(Unity.Jobs.JobHandle), test.World.Unmanaged.ResolveSystemStateRef(system).Dependency);
            var bricks = Bricks(2); // All-disabled chunks can schedule an empty job without synchronizing writers (SPEC-10 Q39).
            Tick(); Assert.AreEqual(0, mesh.Count); Assert.AreEqual(0, collider.Requests.Length);
            foreach (var brick in bricks) Assert.IsFalse(em.IsComponentEnabled<BrickDirty>(brick));
        }
        [Test] public void NoGcAlloc()
        {
            var bricks = Bricks(2);
            for (int i = 0; i < 10; i++) { Dirty(bricks[0]); Tick(); Clear(); }
            Dirty(bricks[0]);
            Assert.That(() => system.Update(test.World.Unmanaged), Is.Not.AllocatingGCMemory());
            mesh.CompleteAccess(); collider.CompleteAccess(); test.CompleteAll(); Assert.AreEqual(1, collider.Requests.Length);
        }
    }
}
