using Game.Core;
using Game.Data.Components;
using Game.Simulation.Commands;
using Game.Simulation.Common;
using Game.Simulation.Flight;
using Game.Simulation.Weapons;
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
    /// <summary>FLT-016 (ADR-0123): experimental 3D flight against the scheduled systems and real thruster content. There is no physics
    /// step here, so each tick the test integrates the pose from the velocity the systems leave, as the physics step would.</summary>
    public sealed class Flight3DTests : GameTestBase
    {
        EcsTestWorld world;
        VoxelTestContent content;
        Entity configEntity;
        EntityManager EM => world.EntityManager;
        const float Mass = 400;
        const float Dt = SimTime.FixedTimestep;

        [SetUp]
        public void SetUp()
        {
            content = new VoxelTestContent();
            world = CreateWorld(typeof(Flight3DModeSystem), typeof(PropulsionSystem), typeof(PlanarConstraintSystem));
            world.AddGameplayActive();
            content.Service.CreateSingletons(EM);
            configEntity = world.AddSingleton(FlightConfig.Default);
        }
        [TearDown] public void TearDown() { DisposeTrackedWorlds(); content?.Dispose(); }

        /// <summary>A small ship with four thrusters through its centre of mass, as in PropulsionTests, plus the player's 3D components.</summary>
        Entity Ship(bool flight3D, quaternion rotation = default, float3 position = default)
        {
            var root = EM.CreateEntity(typeof(ShipCommand), typeof(FlightState), typeof(VoxelGrid), typeof(LocalTransform),
                typeof(PhysicsMass), typeof(PhysicsVelocity), typeof(PlanarConstraint), typeof(ShipTarget));
            EM.SetComponentData(root, new VoxelGrid { CellSize = .5f, Revision = 1 });
            EM.SetComponentData(root, LocalTransform.FromPositionRotation(position, rotation.Equals(default) ? quaternion.identity : rotation));
            EM.SetComponentData(root, new PhysicsMass { Transform = new RigidTransform(quaternion.identity, new float3(.25f)),
                InverseMass = 1 / Mass, InverseInertia = flight3D ? new float3(.01f) : new float3(0, .01f, 0) });
            if (flight3D)
            {
                EM.AddComponent<Flight3D>(root);
                EM.AddComponentData(root, new Flight3DRequest { On = 1 });
            }
            EM.AddBuffer<FlightThruster>(root);
            EM.AddBuffer<BlockInstance>(root);
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

        void Command(Entity root, float lift = 0, ShipCommandFlags flags = ShipCommandFlags.None, float turn = 0)
            => EM.SetComponentData(root, new ShipCommand { Lift = lift, Flags = flags, Turn = turn });

        /// <summary>One fixed tick, then the physics step's integration: position by the linear velocity, rotation by the angular velocity
        /// (inertia frame, identity here, so the ship's own frame).</summary>
        void Tick(int count, params Entity[] ships)
        {
            for (int t = 0; t < count; t++)
            {
                world.StepTicks(1);
                world.CompleteAll();
                foreach (var ship in ships)
                {
                    var pose = EM.GetComponentData<LocalTransform>(ship);
                    var velocity = EM.GetComponentData<PhysicsVelocity>(ship);
                    pose.Position += velocity.Linear * Dt;
                    float spin = math.length(velocity.Angular);
                    if (spin > 0) pose.Rotation = math.normalize(math.mul(pose.Rotation, quaternion.AxisAngle(velocity.Angular / spin, spin * Dt)));
                    EM.SetComponentData(ship, pose);
                }
            }
        }

        float3 Nose(Entity ship) => math.rotate(EM.GetComponentData<LocalTransform>(ship).Rotation, new float3(0, 0, 1));
        float PitchDeg(Entity ship) => math.degrees(math.asin(math.clamp(Nose(ship).y, -1, 1)));
        float RollDeg(Entity ship) => math.degrees(Flight3DMath.Roll(EM.GetComponentData<LocalTransform>(ship).Rotation));
        float AngleDeg(float3 a, float3 b) => math.degrees(math.acos(math.clamp(math.dot(math.normalize(a), math.normalize(b)), -1, 1)));

        [Test]
        public void Lift_ReachesVerticalCap()
        {
            var ship = Ship(true);
            var planar = Ship(false, quaternion.identity, new float3(50, 0, 0));
            float cap = FlightConfig.Default.MaxForwardSpeed * FlightConfig.Default.VerticalSpeedFraction;   // 60 m/s
            Command(ship, lift: 1); Command(planar, lift: 1);
            Tick(300, ship, planar);   // 20 m/s² reaches 60 m/s in 3 s; 5 s settles the assist
            Assert.AreEqual(cap, EM.GetComponentData<PhysicsVelocity>(ship).Linear.y, 1f, "Lift climbs at the vertical cap.");
            Assert.Greater(EM.GetComponentData<LocalTransform>(ship).Position.y, 100f);
            Assert.AreEqual(0, EM.GetComponentData<PhysicsVelocity>(planar).Linear.y, 1e-3f, "A ship without Flight3D ignores Lift.");
            Assert.AreEqual(0, EM.GetComponentData<LocalTransform>(planar).Position.y, 1e-3f);

            Command(ship, lift: -1); Tick(30, ship);
            Assert.Less(EM.GetComponentData<PhysicsVelocity>(ship).Linear.y, cap - 5, "Down thrust pushes against the climb.");
            Command(ship); Tick(300, ship);
            Assert.AreEqual(0, EM.GetComponentData<PhysicsVelocity>(ship).Linear.y, .5f, "Released, the ship holds its height.");
            Assert.Less(math.abs(PitchDeg(ship)), .5f, "Vertical flight keeps the nose level.");
        }

        [Test]
        public void FaceTarget_ConvergesWithoutFlip()
        {
            var ship = Ship(true);
            var target = EM.CreateEntity(typeof(LocalTransform));
            EM.SetComponentData(target, LocalTransform.FromPosition(new float3(150, 200, 200)));
            EM.SetComponentData(ship, new ShipTarget { Value = target });
            Command(ship, flags: ShipCommandFlags.FaceTarget);
            float maxPitch = 0, maxRoll = 0;
            for (int t = 0; t < 360; t++)
            {
                Tick(1, ship);
                maxPitch = math.max(maxPitch, PitchDeg(ship)); maxRoll = math.max(maxRoll, math.abs(RollDeg(ship)));
            }
            float3 toTarget = EM.GetComponentData<LocalTransform>(target).Position - EM.GetComponentData<LocalTransform>(ship).Position;
            Assert.Less(AngleDeg(Nose(ship), toTarget), 2f, "The nose locks onto the target.");
            Assert.Less(maxPitch, 45f, "No overshoot past the target's 39° elevation by more than a few degrees.");
            Assert.Less(maxRoll, 6f, "Roll swings only a little while the nose yaws and pitches at once.");
            Assert.Less(math.abs(RollDeg(ship)), .5f, "Roll settles level.");

            // Straight overhead: the nose stops at MaxPitchDeg instead of flipping over.
            EM.SetComponentData(target, LocalTransform.FromPosition(EM.GetComponentData<LocalTransform>(ship).Position + new float3(0, 2000, .1f)));
            maxPitch = 0;
            for (int t = 0; t < 600; t++) { Tick(1, ship); maxPitch = math.max(maxPitch, PitchDeg(ship)); }
            Assert.AreEqual(FlightConfig.Default.MaxPitchDeg, PitchDeg(ship), 1.5f);
            Assert.Less(maxPitch, FlightConfig.Default.MaxPitchDeg + 2f);

            // HeadingResponse is the lag: a lazier ship has turned less after the same second.
            float Turned(float response)
            {
                var config = FlightConfig.Default; config.HeadingResponse = response; EM.SetComponentData(configEntity, config);
                var s = Ship(true); var aim = EM.CreateEntity(typeof(LocalTransform));
                EM.SetComponentData(aim, LocalTransform.FromPosition(new float3(1000, 300, 0)));
                EM.SetComponentData(s, new ShipTarget { Value = aim });
                Command(s, flags: ShipCommandFlags.FaceTarget);
                Tick(60, s);
                return AngleDeg(new float3(0, 0, 1), Nose(s));
            }
            Assert.Less(Turned(1), Turned(6), "Higher HeadingResponse turns faster.");
        }

        [Test]
        public void Disable_LevelsAndReturnsToPlane()
        {
            var tilted = math.mul(quaternion.RotateY(.4f), math.mul(quaternion.RotateX(-.5f), quaternion.RotateZ(.2f)));
            var ship = Ship(true, tilted, new float3(0, 40, 0));
            Tick(1, ship);
            Assert.IsTrue(EM.IsComponentEnabled<Flight3D>(ship), "Requested on: in 3D.");

            EM.SetComponentData(ship, new Flight3DRequest { On = 0 });
            Command(ship, lift: 1, flags: ShipCommandFlags.FaceAim);   // ignored while levelling
            int ticks = 0;
            while (EM.IsComponentEnabled<Flight3D>(ship) && ticks < 900) { Tick(1, ship); ticks++; }
            Assert.IsFalse(EM.IsComponentEnabled<Flight3D>(ship), "The mode hands the ship back once level.");
            Assert.Less(ticks, 600, "Levelling from 29° pitch and 11° roll takes a few seconds at HeadingResponse 3.");
            var pose = EM.GetComponentData<LocalTransform>(ship);
            Assert.Less(math.abs(Nose(ship).y), Flight3DModeSystem.LevelSine);
            Assert.Less(math.abs(math.rotate(pose.Rotation, new float3(1, 0, 0)).y), Flight3DModeSystem.LevelSine);
            Assert.AreEqual(.4f, Planar.YawFromForward(Nose(ship)), .05f, "Levelling keeps the heading.");

            // Back under the plane lock, the plane pull takes it down to Y = 0.
            float height = pose.Position.y;
            Tick(120, ship);
            Assert.Less(math.abs(EM.GetComponentData<LocalTransform>(ship).Position.y), math.max(.5f, height * .05f));
        }
    }
}
