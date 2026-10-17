using Game.Core;
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
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>HAZ-002: an external push is carried by the flight assist and bleeds off, instead of being cancelled (SPEC-91 §5.1).</summary>
    public sealed class CarryTests : GameTestBase
    {
        EcsTestWorld world;
        VoxelTestContent content;
        EntityManager EM => world.EntityManager;
        const float Mass = 400;

        [SetUp]
        public void SetUp()
        {
            content = new VoxelTestContent();
            world = CreateWorld(typeof(PropulsionSystem));
            world.AddGameplayActive();
            world.AddSimClock(10);
            content.Service.CreateSingletons(EM);
            var config = FlightConfig.Default;
            config.CarryDecel = 8; config.CarryBrakeDecel = 30; config.CarryYawDecel = 2.1f; config.MaxCarry = 120; config.MaxCarryYaw = 3.5f;
            world.AddSingleton(config);
        }
        [TearDown] public void TearDown() { DisposeTrackedWorlds(); content?.Dispose(); }

        Entity Ship()
        {
            var root = EM.CreateEntity(typeof(ShipCommand), typeof(FlightState), typeof(VoxelGrid), typeof(LocalTransform),
                typeof(PhysicsMass), typeof(PhysicsVelocity), typeof(ExternalPush));
            EM.SetComponentData(root, new VoxelGrid { CellSize = .5f, Revision = 1 });
            EM.SetComponentData(root, LocalTransform.Identity);
            EM.SetComponentData(root, new PhysicsMass { Transform = new RigidTransform(quaternion.identity, new float3(.25f)),
                InverseMass = 1 / Mass, InverseInertia = new float3(0, .01f, 0) });
            EM.AddBuffer<FlightThruster>(root);
            EM.AddBuffer<BlockInstance>(root);
            // Four thrusters through the centre of mass, one per planar direction.
            for (int i = 0; i < 4; i++)
            {
                var functional = EM.CreateEntity(typeof(ThrusterState));
                byte orientation = content.Service.Orientations.Value.AxisTurn(1, i);
                float3 offset = GridSpaceMath.BlockDirToGrid(new float3(-1, 0, 0), ref content.Service.Orientations.Value, orientation);
                EM.GetBuffer<BlockInstance>(root).Add(new BlockInstance { Block = content.Index("block_thruster_medium"), Orient = orientation,
                    Origin = (int3)math.round(offset), Functional = functional });
            }
            return root;
        }
        uint Tick => world.GetSingleton<Game.Core.Components.SimClock>().Tick;
        void Shove(Entity root, float3 dv)
        {
            var v = EM.GetComponentData<PhysicsVelocity>(root); v.Linear += dv; EM.SetComponentData(root, v);
            EM.SetComponentData(root, new ExternalPush { LinearDv = dv, Tick = Tick - 1 });
        }
        float Speed(Entity root) => math.length(EM.GetComponentData<PhysicsVelocity>(root).Linear);

        [Test]
        public void SidewaysShove_Carries()
        {
            var root = Ship(); world.StepTicks(2);
            Shove(root, new float3(5, 0, 0));
            world.StepTicks(18);
            Assert.GreaterOrEqual(EM.GetComponentData<PhysicsVelocity>(root).Linear.x, 2.5f, "Still carried after 0.3 s");
            world.StepTicks(42);
            Assert.Less(Speed(root), .1f, "Gone within 1 s");
        }

        [Test]
        public void Brake_ClearsCarry()
        {
            var root = Ship(); world.StepTicks(2);
            Shove(root, new float3(5, 0, 0));
            EM.SetComponentData(root, new ShipCommand { Flags = ShipCommandFlags.Brake });
            world.StepTicks(18);
            Assert.AreEqual(0, EM.GetComponentData<FlightState>(root).Carry.x, 1e-4f, "Brake clears 5 m/s of carry in under 0.3 s");
        }

        [Test]
        public void WallContact_DoesNotDriveIn()
        {
            // Against a wall the measured velocity into it is zero, so no carry survives the clamp (SPEC-91 decision 3).
            Assert.AreEqual(0, FlightMath.ClampCarry(new float2(5, 0), new float2(0, 0), 120).x);
            Assert.AreEqual(new float2(3, 0), FlightMath.ClampCarry(new float2(5, 0), new float2(3, 0), 120));
            Assert.AreEqual(0, FlightMath.ClampCarry(new float2(5, 0), new float2(-2, 0), 120).x, "Opposite sign drops it");
            Assert.AreEqual(4, FlightMath.ClampCarry(5f, 9f, 4f), "Max clamp");
        }

        [Test]
        public void YawKick_Carries()
        {
            var root = Ship(); world.StepTicks(2);
            var v = EM.GetComponentData<PhysicsVelocity>(root); v.Angular = new float3(0, 1, 0); EM.SetComponentData(root, v);
            EM.SetComponentData(root, new ExternalPush { YawDv = 1, Tick = Tick - 1 });
            world.StepTicks(1);
            Assert.Greater(EM.GetComponentData<FlightState>(root).CarryYaw, .9f);
        }

        [Test]
        public void Deterministic()
        {
            float3 Run()
            {
                var root = Ship(); world.StepTicks(2); Shove(root, new float3(3, 0, -4)); world.StepTicks(30);
                return EM.GetComponentData<PhysicsVelocity>(root).Linear;
            }
            var a = Run(); var b = Run();
            Assert.AreEqual(a, b);
        }
    }
}
