using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Simulation.Commands;
using Game.Simulation.Common;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Tests.Common;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Flight
{
    public abstract class PropulsionIntegrationFixture : GameTestBase
    {
        protected EcsTestWorld World;
        protected VoxelTestContent Content;
        protected EntityManager EM => World.EntityManager;
        [SetUp] public void SetUp()
        {
            Content = new VoxelTestContent();
            World = CreateWorld(typeof(ThrusterPowerDemandSystem), typeof(PropulsionStatsSystem), typeof(PropulsionSystem));
            World.AddGameplayActive(); Content.Service.CreateSingletons(EM); World.AddSingleton(FlightConfig.Default);
        }
        [TearDown] public void TearDown() { DisposeTrackedWorlds(); Content?.Dispose(); }
        protected Entity Ship(int thrusterCount = 4)
        {
            var root = EM.CreateEntity(typeof(ShipCommand), typeof(FlightState), typeof(VoxelGrid), typeof(LocalTransform),
                typeof(PhysicsMass), typeof(PhysicsVelocity), typeof(ShipStats), typeof(ShipStatMultipliers), typeof(PropulsionStats));
            EM.SetComponentData(root, LocalTransform.Identity);
            EM.SetComponentData(root, new VoxelGrid { CellSize = .5f, Revision = 1 });
            EM.SetComponentData(root, new PhysicsMass { Transform = new RigidTransform(quaternion.identity, new float3(.25f)),
                InverseMass = 1f / 400, InverseInertia = new float3(0, .01f, 0) });
            EM.SetComponentData(root, new ShipStats { Mass = 400, FunctionalRevision = 1 });
            EM.AddBuffer<FlightThruster>(root); EM.AddBuffer<BlockInstance>(root);
            for (int i = 0; i < thrusterCount; i++)
            {
                var functional = EM.CreateEntity(typeof(FunctionalBlock), typeof(ThrusterState), typeof(Powered));
                EM.SetComponentData(functional, new FunctionalBlock { Grid = root, InstanceIndex = i });
                var requests = EM.AddBuffer<PowerRequest>(functional);
                PowerRegistration.SetConsumer(requests, BlockModuleId.Thruster, 1, 150);
                byte orientation = Content.Service.Orientations.Value.AxisTurn(1, i);
                float3 offset = GridSpaceMath.BlockDirToGrid(new float3(-1, 0, 0), ref Content.Service.Orientations.Value, orientation);
                EM.GetBuffer<BlockInstance>(root).Add(new BlockInstance { Block = Content.Index("block_thruster_medium"),
                    Orient = orientation, Origin = (int3)math.round(offset), Functional = functional });
            }
            return root;
        }
        protected void Tick(int count = 1) => World.StepTicks(count);
        protected void Multiplier(Entity root, StatId id, float value)
        {
            var multipliers = EM.GetComponentData<ShipStatMultipliers>(root);
            while (multipliers.Values.Length <= (int)id) multipliers.Values.Add(1);
            multipliers.Values[(int)id] = value; EM.SetComponentData(root, multipliers);
        }
        protected void Forward(Entity root) => EM.SetComponentData(root, new ShipCommand { Thrust = new float2(0, 1) });
        protected float Speed(Entity root) => EM.GetComponentData<PhysicsVelocity>(root).Linear.z;
        protected PropulsionStats Stats(Entity root) => EM.GetComponentData<PropulsionStats>(root);
    }

    public sealed class PropulsionPowerTests : PropulsionIntegrationFixture
    {
        [Test] public void ThrustMultiplier_Applies()
        {
            var normal = Ship(); var boosted = Ship(); Multiplier(boosted, StatId.Thrust, 2);
            Forward(normal); Forward(boosted); Tick();
            Assert.Greater(Speed(normal), 0); Assert.That(Speed(boosted), Is.EqualTo(2 * Speed(normal)).Within(.0001f));
        }
        [Test] public void MaxSpeedMultiplier_Applies()
        {
            var root = Ship(); Multiplier(root, StatId.MaxSpeed, 1.5f); Forward(root); Tick(240);
            Assert.That(Speed(root), Is.EqualTo(180).Within(.1f));
            Assert.AreEqual(180, EM.GetComponentData<FlightState>(root).ForwardCap);
        }
        [Test] public void Unpowered_DoesNotFire()
        {
            var root = Ship();
            foreach (var instance in EM.GetBuffer<BlockInstance>(root)) EM.SetComponentEnabled<Powered>(instance.Functional, false);
            Forward(root); Tick(); Assert.AreEqual(0, Speed(root));
            foreach (var thruster in EM.GetBuffer<FlightThruster>(root))
            { Assert.AreEqual(0, thruster.Throttle); Assert.AreEqual(0, EM.GetComponentData<ThrusterState>(thruster.Thruster).Intensity); }
        }
        [Test] public void Demand_SameTick()
        {
            var root = Ship(); Tick(); Check(0); Forward(root); Tick(); Check(1);
            EM.SetComponentData(root, default(ShipCommand)); Tick(); Check(1); // Coast still uses assist.
            EM.SetComponentData(root, default(PhysicsVelocity)); Tick(); Check(0);
            EM.SetComponentData(root, new PhysicsVelocity { Angular = new float3(0, 1, 0) }); Tick(); Check(1);
            void Check(byte expected)
            { foreach (var instance in EM.GetBuffer<BlockInstance>(root)) Assert.AreEqual(expected, EM.GetBuffer<PowerRequest>(instance.Functional)[0].Requesting); }
        }
    }
}
