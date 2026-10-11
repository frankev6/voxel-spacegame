using Game.Core;
using Game.Data.Blocks;
using Game.Simulation.Commands;
using Game.Simulation.Common;
using Game.Simulation.Flight;
using Game.Tests.Common;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>Eight required FLT-007 tests against the scheduled system and real module blobs.</summary>
    public sealed class PropulsionTests : GameTestBase
    {
        EcsTestWorld world;
        VoxelTestContent content;
        Entity configEntity;
        EntityManager EM => world.EntityManager;
        const float Mass = 400;
        const float Force = 40000;

        [SetUp]
        public void SetUp()
        {
            content = new VoxelTestContent();
            world = CreateWorld(typeof(PropulsionSystem));
            world.AddGameplayActive();
            content.Service.CreateSingletons(EM);
            configEntity = world.AddSingleton(FlightConfig.Default);
        }
        [TearDown] public void TearDown() { DisposeTrackedWorlds(); content?.Dispose(); }

        Entity Ship()
        {
            var root = EM.CreateEntity(typeof(ShipCommand), typeof(FlightState), typeof(VoxelGrid), typeof(LocalTransform),
                typeof(PhysicsMass), typeof(PhysicsVelocity));
            EM.SetComponentData(root, new VoxelGrid { CellSize = .5f, Revision = 1 });
            EM.SetComponentData(root, LocalTransform.Identity);
            EM.SetComponentData(root, new PhysicsMass { Transform = new RigidTransform(quaternion.identity, new float3(.25f)),
                InverseMass = 1 / Mass, InverseInertia = new float3(0, .01f, 0) });
            EM.AddBuffer<FlightThruster>(root);
            EM.AddBuffer<BlockInstance>(root);
            // Four real orientations, with each force line passing through the centre of mass.
            for (int i = 0; i < 4; i++)
            {
                var functional = EM.CreateEntity(typeof(ThrusterState));
                byte orientation = content.Service.Orientations.Value.AxisTurn(1, i);
                float3 offset = GridSpaceMath.BlockDirToGrid(new float3(-1, 0, 0), ref content.Service.Orientations.Value, orientation);
                EM.GetBuffer<BlockInstance>(root).Add(new BlockInstance
                {
                    Block = content.Index("block_thruster_medium"), Orient = orientation,
                    Origin = (int3)math.round(offset), Functional = functional
                });
            }
            return root;
        }
        void Tick(int count = 1) => world.StepTicks(count);
        PhysicsVelocity Velocity(Entity root) => EM.GetComponentData<PhysicsVelocity>(root);
        void Command(Entity root, float2 thrust = default, float turn = 0, ShipCommandFlags flags = ShipCommandFlags.None)
            => EM.SetComponentData(root, new ShipCommand { Thrust = thrust, Turn = turn, Flags = flags });

        [Test]
        public void ThrusterList_RebuildOnRevision()
        {
            var root = Ship();
            var config = FlightConfig.Default; config.AllocationSweeps = 0;
            EM.SetComponentData(configEntity, config);
            Tick();
            var buffer = EM.GetBuffer<FlightThruster>(root);
            Assert.AreEqual(4, buffer.Length);
            for (int i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i]; entry.Throttle = .1f * (i + 1); buffer[i] = entry;
                Assert.AreEqual(i, entry.InstanceIndex);
                Assert.AreEqual(Force, entry.MaxForce);
                Assert.Less(math.length(math.cross(entry.PositionLocal - new float3(.25f), entry.ForceDirLocal)), .00001f);
            }
            var instance = EM.GetBuffer<BlockInstance>(root)[1]; instance.Flags = BlockInstanceFlags.Destroyed;
            var instances = EM.GetBuffer<BlockInstance>(root); instances[1] = instance;
            Tick(); // Same revision: no geometry scan, even though the test changed an instance.
            Assert.AreEqual(4, EM.GetBuffer<FlightThruster>(root).Length);
            var grid = EM.GetComponentData<VoxelGrid>(root); grid.Revision++; EM.SetComponentData(root, grid);
            Tick();
            buffer = EM.GetBuffer<FlightThruster>(root);
            Assert.AreEqual(3, buffer.Length);
            for (int k = 0; k < buffer.Length; k++) Assert.AreEqual(.1f * (buffer[k].InstanceIndex + 1), buffer[k].Throttle);
            Assert.AreEqual(grid.Revision, EM.GetComponentData<FlightState>(root).SourceRevision);
            // Missing entities cannot fire even before the next geometry revision.
            var missing = buffer[0].Thruster; EM.DestroyEntity(missing); Tick();
            Assert.AreEqual(0, EM.GetBuffer<FlightThruster>(root)[0].Throttle);
            // Optional spawn gate and kinematic body both leave state and velocity untouched.
            var pending = Ship(); EM.AddComponent<GridSpawning>(pending); Command(pending, new float2(0, 1));
            var kinematic = Ship(); var mass = EM.GetComponentData<PhysicsMass>(kinematic); mass.InverseMass = 0;
            EM.SetComponentData(kinematic, mass); Command(kinematic, new float2(0, 1)); Tick();
            Assert.AreEqual(0, EM.GetBuffer<FlightThruster>(pending).Length);
            Assert.AreEqual(default(PhysicsVelocity), Velocity(pending));
            Assert.AreEqual(default(PhysicsVelocity), Velocity(kinematic));
        }

        [Test]
        public void Forward_ReachesCap()
        {
            var root = Ship(); Command(root, new float2(0, 1)); Tick(180);
            float cap = FlightConfig.Default.MaxForwardSpeed;
            Assert.That(Velocity(root).Linear.z, Is.EqualTo(cap).Within(cap * .01f));
            Tick(120); Assert.That(Velocity(root).Linear.z, Is.EqualTo(cap).Within(cap * .01f));
            Assert.Less(math.abs(Velocity(root).Angular.y), .00001f);
        }
        [Test]
        public void Coast_KeepsSpeed()
        {
            var root = Ship(); Command(root, new float2(0, 1)); Tick(100);
            float initial = Velocity(root).Linear.z;
            Command(root); Tick(120);
            Assert.That(Velocity(root).Linear.z, Is.EqualTo(initial).Within(initial * .005f));
        }
        [Test]
        public void Brake_Stops()
        {
            var root = Ship();
            // Unit assist gain isolates the M*v/F saturated stopping prediction from the default exponential tail.
            var config = FlightConfig.Default; config.LinearAssistGain = 1;
            EM.SetComponentData(configEntity, config);
            EM.SetComponentData(root, new PhysicsVelocity { Linear = new float3(0, 0, 30) });
            Command(root, flags: ShipCommandFlags.Brake | ShipCommandFlags.Boost);
            int ticks = 0;
            while (math.abs(Velocity(root).Linear.z) > .001f && ticks < 120) { Tick(); ticks++; }
            Assert.That(ticks, Is.EqualTo(Mass * 30 / Force / SimTime.FixedTimestep).Within(2));
            Assert.Less(math.length(Velocity(root).Linear), .001f);
            Assert.AreEqual(FlightConfig.Default.MaxForwardSpeed, EM.GetComponentData<FlightState>(root).ForwardCap);
            // The shipped half-gain assist also converges, with its documented final unsaturated tail.
            EM.SetComponentData(configEntity, FlightConfig.Default);
            EM.SetComponentData(root, new PhysicsVelocity { Linear = new float3(0, 0, 30) }); Tick(60);
            Assert.Less(math.length(Velocity(root).Linear), .01f);
        }
        [Test]
        public void Assist_KillsDrift()
        {
            var root = Ship(); EM.SetComponentData(root, new PhysicsVelocity { Linear = new float3(25, 0, 20) });
            Tick(120); Assert.Less(math.abs(Velocity(root).Linear.x), .01f);
            Assert.That(Velocity(root).Linear.z, Is.EqualTo(20).Within(.01f));
        }
        [Test]
        public void Assist_StopsSpin()
        {
            var root = Ship(); EM.SetComponentData(root, new PhysicsVelocity { Angular = new float3(0, 1, 0) });
            Tick(120); Assert.Less(math.abs(Velocity(root).Angular.y), .001f);
            Assert.Greater(EM.GetComponentData<FlightState>(root).GyroTorque, 0);
        }
        [Test]
        public void NoVelocityAssignment()
        {
            var root = Ship(); var mass = EM.GetComponentData<PhysicsMass>(root);
            var velocity = default(PhysicsVelocity);
            velocity.ApplyLinearImpulse(mass, new float3(20, 7, 30) * Mass);
            EM.SetComponentData(root, velocity); Tick();
            var after = Velocity(root);
            Assert.That(after.Linear.x, Is.InRange(20 - Force / Mass * SimTime.FixedTimestep - .0001f, 20));
            Assert.AreEqual(7, after.Linear.y);
            Assert.That(after.Linear.z, Is.EqualTo(30).Within(.0001f));
            Assert.Greater(after.Linear.x, 0);
        }
        [Test]
        public void Hulk_IdlesThrusters()
        {
            var root = Ship(); Command(root, new float2(0, 1)); Tick();
            var thrusters = EM.GetBuffer<FlightThruster>(root).ToNativeArray(Unity.Collections.Allocator.Temp);
            bool lit = false;
            foreach (var thruster in thrusters) lit |= EM.GetComponentData<ThrusterState>(thruster.Thruster).Intensity > 0;
            Assert.IsTrue(lit);
            EM.AddComponent<Hulk>(root); var before = Velocity(root); Tick();
            foreach (var thruster in thrusters) Assert.AreEqual(0, EM.GetComponentData<ThrusterState>(thruster.Thruster).Intensity);
            Assert.AreEqual(before, Velocity(root));
            Assert.IsTrue((EM.GetComponentData<FlightState>(root).Flags & FlightFlags.HulkIdled) != 0);
            var sentinel = thrusters[0].Thruster; EM.SetComponentData(sentinel, new ThrusterState { Intensity = .25f });
            Tick(); Assert.AreEqual(.25f, EM.GetComponentData<ThrusterState>(sentinel).Intensity);
            Assert.AreEqual(before, Velocity(root)); thrusters.Dispose();
        }
    }
}
