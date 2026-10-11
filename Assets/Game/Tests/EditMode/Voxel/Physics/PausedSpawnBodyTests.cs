using Game.App.Systems;
using Game.Core.Components;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Voxel.Physics
{
    /// <summary>PHY-024: ready bodies without gameplay ticks, preserving fixed-step split adoption (ADR-0037).</summary>
    public sealed class PausedSpawnBodyTests
    {
        [Test]
        public void FreshSpawn_ReadyWithoutTicks()
        {
            using var f = new GridBodyInitTests.Fixture(false, typeof(GridSpawnBodyInitSystem), typeof(SimClockAdvanceSystem));
            var linear = new float3(3, 0, 7);
            var root = f.Spawn(linear: linear, rotation: quaternion.RotateY(.3f));
            var pose = f.Em.GetComponentData<LocalTransform>(root);
            var clock = f.Test.GetSingleton<SimClock>();
            f.Test.StepFrame(0);
            Assert.AreEqual(0, f.Test.TicksRun);
            Assert.AreEqual(clock, f.Test.GetSingleton<SimClock>());
            Assert.AreEqual(pose, f.Em.GetComponentData<LocalTransform>(root));
            Assert.AreEqual(linear, f.Em.GetComponentData<PhysicsVelocity>(root).Linear);
            Assert.IsTrue(f.Em.GetComponentData<PhysicsCollider>(root).Value.IsCreated);
            Assert.IsFalse(f.Em.IsComponentEnabled<GridBodyInit>(root));
            Assert.AreEqual(GridColliderPhase.Provisional, f.Em.GetComponentData<GridColliderState>(root).Phase);
            var grid = f.Em.GetComponentData<VoxelGrid>(root);
            var mass = f.Em.GetComponentData<GridMass>(root).Data;
            Assert.AreEqual(GridMassMath.CreateDynamicMass(mass, grid.CellMin, grid.CellMax, grid.CellSize, true), f.Em.GetComponentData<PhysicsMass>(root));
            int requests = f.Queue.Requests.Length;
            Assert.Greater(requests, 0);
            foreach (var request in f.Queue.Requests) Assert.AreEqual(ColliderPriority.Spawn, request.Priority);
            f.Test.StepFrame(0);
            Assert.AreEqual(1, f.Cache.C.Grids.Length);
            Assert.AreEqual(requests, f.Queue.Requests.Length);
            Assert.AreEqual(clock, f.Test.GetSingleton<SimClock>());
            ColliderPipelineInvariants.Check(f.Cache, f.Batch);
        }

        [Test]
        public void ExistingBody_StillAdoptedInFixedStep()
        {
            using var f = new GridBodyInitTests.Fixture(false, typeof(GridSpawnBodyInitSystem), typeof(SimClockAdvanceSystem));
            var root = f.Spawn();
            var grid = f.Em.GetComponentData<VoxelGrid>(root);
            var blob = GridBodyFactory.CreateProvisionalCollider(grid.CellMin, grid.CellMax, grid.CellSize, false);
            try
            {
                var velocity = new PhysicsVelocity { Linear = new float3(7, 8, 9), Angular = new float3(1, 2, 3) };
                f.Em.AddComponent(root, GridBodyFactory.DynamicBodyTypes());
                f.Em.AddSharedComponent(root, GridBodyFactory.WorldIndex());
                f.Em.SetComponentData(root, new PhysicsCollider { Value = blob });
                f.Em.SetComponentData(root, velocity);
                f.Test.StepFrame(0);
                Assert.AreEqual(0, f.Test.TicksRun);
                Assert.IsTrue(f.Em.IsComponentEnabled<GridBodyInit>(root));
                Assert.AreEqual(0, f.Cache.C.Grids.Length);
                Assert.AreEqual(0, f.Queue.Requests.Length);
                f.Tick();
                Assert.IsFalse(f.Em.IsComponentEnabled<GridBodyInit>(root));
                Assert.AreEqual(velocity, f.Em.GetComponentData<PhysicsVelocity>(root));
                Assert.AreEqual(blob, f.Cache.C.Grids[0].Installed);
                Assert.Greater(f.Queue.Requests.Length, 0);
                foreach (var request in f.Queue.Requests) Assert.AreEqual(ColliderPriority.Split, request.Priority);
                ColliderPipelineInvariants.Check(f.Cache, f.Batch);
            }
            finally
            {
                // Until adoption the test, like SplitSystem, owns this provisional blob.
                f.Cache.CompleteAccess();
                if (!f.Cache.C.GridIndex.ContainsKey(grid.Serial)) blob.Dispose();
            }
        }
    }
}
