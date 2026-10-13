using System;
using Game.Galaxy.Components;
using Game.Presentation.Data;
using Game.Presentation.Systems;
using Game.Simulation.Commands;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using CameraFixture = Game.Tests.EditMode.Presentation.CameraRigServiceTests.Fixture;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>Five required CAM-006 scenarios through the production camera bridge.</summary>
    public sealed class CameraOriginShiftTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly CameraFixture Camera = new CameraFixture();
            public readonly EcsTestWorld World = EcsTestWorld.Create(typeof(CameraTargetSyncSystem));
            public readonly Entity Ship, Origin;
            public float3 Position = new float3(2000, 0, 20);
            public EntityManager Em => World.EntityManager;
            public Fixture(bool origin = true)
            {
                Ship = Em.CreateEntity(typeof(PlayerShip), typeof(LocalToWorld));
                Origin = origin ? Em.CreateEntity(typeof(FrameOrigin)) : Entity.Null;
                Camera.Rig.SetMode(CameraMode.Flight, true);
                World.World.GetExistingSystemManaged<CameraTargetSyncSystem>().Bind(Camera.Rig, null);
                Step();
            }
            public float3 Offset => (float3)Camera.Rig.Camera.transform.position - Position;
            public void Step()
            {
                Em.SetComponentData(Ship, new LocalToWorld { Value = float4x4.Translate(Position) });
                World.StepFrame(0);
            }
            public void Shift(double3 origin)
            {
                var previous = Em.GetComponentData<FrameOrigin>(Origin);
                Position += (float3)(previous.OriginInStarSystem - origin);
                Em.SetComponentData(Origin, new FrameOrigin { OriginInStarSystem = origin });
                Step();
            }
            public void Dispose() { World.Dispose(); Camera.Dispose(); }
        }
        [Test] public void Warp_OffsetPreserved_SameFrame()
        {
            using var f = new Fixture(); var before = f.Offset;
            f.Shift(new double3(2000, 0, 0));
            Assert.That(math.distance(before, f.Offset), Is.LessThan(.001f));
            Assert.That(math.distance(f.Camera.Rig.ListenerPivot.position, f.Camera.Rig.Focus), Is.LessThan(.001f));
        }
        [Test] public void Warp_NoTransient_Next10Frames()
        {
            using var f = new Fixture(); var before = f.Offset;
            f.Shift(new double3(2000, 0, 0));
            for (int i = 0; i < 10; i++)
            {
                f.Step();
                Assert.That(math.distance(before, f.Offset), Is.LessThan(.001f), $"Frame {i}");
            }
        }
        [Test] public void TargetJumpWithoutOriginChange_Cuts()
        {
            using var f = new Fixture(); f.Position += new float3(600, 0, 0); f.Step();
            Assert.AreEqual(f.Position, f.Camera.Rig.FocusState);
            // Destination entry also cuts in the same bridge update, without an intermediate million-metre view.
            f.Em.SetComponentData(f.Origin, new FrameOrigin { OriginInStarSystem = new double3(1000000, 0, 0) });
            f.Position = new float3(30, 0, 70); f.Step();
            Assert.AreEqual(f.Position, f.Camera.Rig.FocusState);
            var expected = Game.Presentation.Services.CameraMath.OrbitPosition(f.Position,
                Game.Presentation.Services.CameraMath.OrbitRotation(55, 0), 150);
            Assert.That(math.distance(expected, f.Camera.Rig.Camera.transform.position), Is.LessThan(.001f));
        }
        [Test] public void OriginChangeWithMatchingTargetShift_DoesNotCut()
        {
            using var f = new Fixture();
            f.Position += new float3(10, 0, 0);
            f.Camera.Rig.SetTarget(f.Position, quaternion.identity, float3.zero, 10, false);
            f.Camera.Rig.Tick(.01f);
            f.Shift(new double3(2000, 0, 0));
            Assert.That(math.distance(f.Camera.Rig.FocusState, f.Position), Is.GreaterThan(1), "An origin warp must preserve unfinished follow damping rather than cutting it.");
        }
        [Test] public void NoFrameOrigin_NoWarp()
        {
            using var f = new Fixture(false); var before = f.Offset; f.Step();
            Assert.That(math.distance(before, f.Offset), Is.LessThan(.001f));
            var origin = f.Em.CreateEntity(typeof(FrameOrigin));
            f.Em.SetComponentData(origin, new FrameOrigin { OriginInStarSystem = new double3(1000000) }); f.Step();
            Assert.That(math.distance(before, f.Offset), Is.LessThan(.001f), "First observation establishes the origin baseline.");
            f.Em.DestroyEntity(origin); f.Step();
            Assert.That(math.distance(before, f.Offset), Is.LessThan(.001f));
        }
    }
}
