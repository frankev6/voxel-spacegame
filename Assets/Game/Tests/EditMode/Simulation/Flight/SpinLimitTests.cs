using Game.Simulation.Flight;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>RACE-016: a flown ship's yaw rate after a collision is held to SpinLimitSystem.SpinFactor × its turn cap.</summary>
    public sealed class SpinLimitTests : GameTestBase
    {
        EcsTestWorld world;
        EntityManager EM => world.EntityManager;

        [SetUp] public void SetUp() { world = CreateWorld(typeof(SpinLimitSystem)); world.AddGameplayActive(); world.AddSimClock(1); }
        [TearDown] public void TearDown() => DisposeTrackedWorlds();

        Entity Ship(float yawRate, float cap, quaternion rotation)
        {
            var e = EM.CreateEntity(typeof(LocalTransform), typeof(PhysicsVelocity), typeof(PhysicsMass), typeof(FlightState));
            EM.SetComponentData(e, LocalTransform.FromPositionRotation(float3.zero, rotation));
            // a collision's spin: Angular is in inertia space, here the body's own frame
            EM.SetComponentData(e, new PhysicsVelocity { Linear = new float3(0, 0, 190), Angular = math.rotate(math.inverse(rotation), new float3(0, yawRate, 0)) });
            EM.SetComponentData(e, new PhysicsMass { Transform = RigidTransform.identity, InverseMass = 1f / 3000, InverseInertia = new float3(1e-4f) });
            EM.SetComponentData(e, new FlightState { YawRateCap = cap });
            return e;
        }
        float Yaw(Entity e)
        {
            var pose = EM.GetComponentData<LocalTransform>(e);
            return math.rotate(pose.Rotation, EM.GetComponentData<PhysicsVelocity>(e).Angular).y;
        }

        [Test]
        public void CollisionSpin_HeldToTheCap()
        {
            var e = Ship(22, 2, quaternion.RotateY(1.1f));
            world.StepTicks(1);
            Assert.AreEqual(2 * SpinLimitSystem.SpinFactor, Yaw(e), 1e-3f, "a 22 rad/s contact spin is cut to 1.6 × the 2 rad/s turn cap");
            Assert.AreEqual(190, EM.GetComponentData<PhysicsVelocity>(e).Linear.z, 1e-3f, "linear velocity is untouched");
        }

        [Test]
        public void NormalTurn_Untouched()
        {
            var e = Ship(-1.5f, 2, quaternion.identity);
            world.StepTicks(1);
            Assert.AreEqual(-1.5f, Yaw(e), 1e-4f);
        }
    }
}
