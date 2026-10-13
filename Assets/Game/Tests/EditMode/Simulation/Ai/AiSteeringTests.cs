using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Simulation;
using Game.Simulation.Abilities;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Common;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Ai
{
    /// <summary>AI-007's four required tests, including the real decision and physical propulsion paths.</summary>
    public sealed class AiSteeringTests : GameTestBase
    {
        static ShipCommand Stale => new ShipCommand { Thrust = new float2(-.8f, -.7f), Turn = -1,
            Flags = ShipCommandFlags.Brake | ShipCommandFlags.Boost, Edges = (ShipCommandEdges)ushort.MaxValue,
            FireGroups = uint.MaxValue, AimWorld = new float3(-999), AimValid = 1 };

        [Test] public void RewritesEveryTick()
        {
            using var f = new Fixture(decisions: true);
            var ship = f.Ship(); f.Grid(new float3(0, 0, 100), 1);
            f.Em.SetComponentData(ship, new ShipStats { MassFraction = .1f });
            var slots = f.Em.AddBuffer<AbilitySlot>(ship);
            for (int i = 0; i < 4; i++) slots.Add(new AbilitySlot { AbilityIndex = (ushort)(i + 1),
                State = i == 1 ? AbilitySlotState.Cooldown : AbilitySlotState.Ready, Blocked = (byte)(i == 3 ? 1 : 0) });
            var player = f.Ship(); f.Em.AddComponent<PlayerShip>(player); f.Em.SetComponentData(player, Stale);
            var hulk = f.Ship(); f.Em.AddComponent<Hulk>(hulk); f.Em.SetComponentData(hulk, Stale);
            var dormant = f.Ship(); f.Em.AddComponent<Game.Voxel.Rendering.Dormant>(dormant);
            f.Em.SetComponentData(dormant, Stale);
            for (uint tick = 1; tick <= 12; tick++)
            {
                f.Em.SetComponentData(ship, Stale); f.Em.SetComponentData(dormant, Stale);
                f.Tick(); var command = f.Command(ship); var brain = f.Brain(ship);
                Assert.AreEqual(0, command.FireGroups); Assert.AreEqual(ShipCommandFlags.None, command.Flags);
                Assert.IsTrue(math.all(math.abs(command.Thrust) <= 1)); Assert.That(command.Turn, Is.InRange(-1f, 1f));
                bool think = tick % 4 == 0;
                Assert.AreEqual(think ? ShipCommandEdges.Ability1 | ShipCommandEdges.Ability3 : ShipCommandEdges.None, command.Edges);
                if (think)
                {
                    Assert.AreEqual(AiState.Flee, brain.State); Assert.AreEqual(5, brain.AbilityEdges);
                    Assert.AreEqual(tick, brain.AbilityEdgesTick); Assert.AreEqual(tick, brain.LastThinkTick);
                }
                else if (tick > 4) Assert.AreNotEqual(tick, brain.AbilityEdgesTick, "A retained brain edge must not repeat between thinks.");
                Assert.AreEqual(tick / 4, brain.ThinkCount);
                Assert.AreEqual(tick >= 4 ? 1 : 0, command.AimValid);
                if (tick >= 4) Assert.AreEqual(new float3(0, 0, 100), command.AimWorld);
                Assert.AreEqual(Stale, f.Command(player)); Assert.AreEqual(Stale, f.Command(hulk));
                Assert.AreEqual(0, f.Command(dormant).FireGroups, "Dormancy slows thinking, never command rewriting.");
                Assert.Greater(f.Command(dormant).Thrust.y, 0);
            }
        }

        [Test] public void ArrivesAndStops()
        {
            using var f = new Fixture(physics: true);
            var ship = f.PhysicalShip(); var goal = new float3(0, 0, 500);
            var brain = f.Brain(ship); brain.GoalPosition = goal; brain.GoalSpeedFraction = .85f;
            f.Em.SetComponentData(ship, brain);
            float peakSpeed = 0, maxForward = 0; bool reverseFired = false, arrived = false;
            for (int tick = 0; tick < 1800; tick++)
            {
                f.Tick(); var pose = f.Em.GetComponentData<LocalTransform>(ship);
                float speed = math.length(f.Em.GetComponentData<PhysicsVelocity>(ship).Linear);
                peakSpeed = math.max(peakSpeed, speed); maxForward = math.max(maxForward, pose.Position.z);
                Assert.LessOrEqual(maxForward, 520, "The real flight pipeline must not overshoot the 500m goal by more than 20m.");
                foreach (var thruster in f.Em.GetBuffer<FlightThruster>(ship))
                    reverseFired |= thruster.ForceDirLocal.z < -.9f && thruster.Throttle > 0;
                if (math.distance(pose.Position, goal) >= Fixture.Arrival || speed >= 1) continue;
                arrived = true; break;
            }
            Assert.IsTrue(arrived, "A moving goal follower must arrive and stop within 30 simulated seconds.");
            Assert.Greater(peakSpeed, 10, "Real thrusters must first accelerate the body.");
            Assert.IsTrue(reverseFired, "Stopping must use opposing production thrusters.");
            Assert.AreEqual(4, f.Em.GetBuffer<FlightThruster>(ship).Length);
            Assert.AreEqual(100, f.Em.GetComponentData<PropulsionStats>(ship).ForwardAccel, .001f);
            // Keep integrating after arrival to detect a one-tick threshold crossing followed by renewed motion.
            for (int tick = 0; tick < 120; tick++)
            {
                f.Tick(); var pose = f.Em.GetComponentData<LocalTransform>(ship);
                Assert.Less(math.distance(pose.Position, goal), Fixture.Arrival);
                Assert.Less(math.length(f.Em.GetComponentData<PhysicsVelocity>(ship).Linear), 1);
                Assert.LessOrEqual(pose.Position.z, 520);
            }
        }

        [Test] public void FacesHeading()
        {
            using var f = new Fixture(); var ship = f.Ship();
            foreach (var row in new[] { (0f, .25f, 2f, .25f), (0f, 2f, 2f, 1f), (0f, -2f, 2f, -1f),
                (2.9f, -2.9f, 2f, 2 * math.PI - 5.8f), (-2.9f, 2.9f, 2f, 5.8f - 2 * math.PI), (0f, 1f, 0f, 0f) })
            {
                f.Em.SetComponentData(ship, LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(row.Item1)));
                var brain = f.Brain(ship); brain.GoalPosition = float3.zero; brain.GoalHeadingYaw = row.Item2;
                f.Em.SetComponentData(ship, brain);
                var flight = f.Em.GetComponentData<FlightState>(ship); flight.YawRateCap = row.Item3;
                f.Em.SetComponentData(ship, flight); f.Tick();
                Assert.AreEqual(row.Item4, f.Command(ship).Turn, .00001f);
                Assert.AreEqual(float2.zero, f.Command(ship).Thrust);
            }
            f.Em.SetComponentData(ship, LocalTransform.Identity);
            var goal = f.Brain(ship); goal.GoalPosition = new float3(1000, 99, 0); goal.GoalSpeedFraction = 1;
            f.Em.SetComponentData(ship, goal); f.Tick();
            Assert.AreEqual(1, f.Command(ship).Thrust.x); Assert.AreEqual(0, f.Command(ship).Thrust.y);
            f.Em.SetComponentData(ship, LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(math.PI * .5f)));
            f.Tick(); Assert.AreEqual(math.sqrt(4 * (1000 - Fixture.Arrival)) / 100, f.Command(ship).Thrust.y, .0001f,
                "Missing propulsion stats use the specified 2m/s² arrival deceleration and rotate the world goal into forward thrust.");
            goal.GoalPosition = new float3(0, 0, 1000); goal.GoalSpeedFraction = .1f; f.Em.SetComponentData(ship, goal);
            f.Em.SetComponentData(ship, LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(math.PI)));
            f.Tick(); Assert.AreEqual(-.25f, f.Command(ship).Thrust.y, .0001f, "A 10m/s backward goal uses the 40m/s reverse cap.");
            f.Em.SetComponentData(ship, LocalTransform.Identity); goal.GoalSpeedFraction = 1; f.Em.SetComponentData(ship, goal);
            f.Em.AddComponentData(ship, new PropulsionStats { ForwardAccel = .2f }); f.Tick();
            Assert.AreEqual(math.sqrt(1000 - Fixture.Arrival) / 100, f.Command(ship).Thrust.y, .0001f,
                "Low measured propulsion retains the specified 0.5m/s² braking floor.");
            // Arrival must force assist instead of neutral coasting for either direction of existing forward motion.
            foreach (float speed in new[] { 12f, -12f })
            {
                goal.GoalPosition = float3.zero; f.Em.SetComponentData(ship, goal);
                var flight = f.Em.GetComponentData<FlightState>(ship); flight.LocalVelocity = new float2(0, speed);
                f.Em.SetComponentData(ship, flight); f.Tick();
                Assert.AreEqual(-.002f * math.sign(speed), f.Command(ship).Thrust.y);
            }
        }

        [Test] public void Turret_NoThrust()
        {
            using var f = new Fixture(); var target = f.Grid(new float3(100, 5, 20), 1);
            var grid = f.Em.GetComponentData<VoxelGrid>(target); grid.CellMin = new int3(2, 0, 4); grid.CellMax = new int3(5, 1, 7);
            f.Em.SetComponentData(target, grid);
            var pose = LocalTransform.FromPositionRotationScale(new float3(100, 5, 20), quaternion.RotateY(.5f), 2);
            f.Em.SetComponentData(target, pose);
            var turret = f.Ship(profile: 1); var station = f.Ship(profile: 1, flight: false);
            var incomplete = f.Ship(flight: false);
            var roots = new[] { turret, station, incomplete };
            foreach (var root in roots)
            {
                f.Em.SetComponentData(root, Stale);
                f.Em.SetComponentData(root, new ShipTarget { Value = target });
                f.Em.SetComponentData(root, new WeaponControl { Autofire = 1, AimMode = WeaponAimMode.TargetLead });
                var brain = f.Brain(root); brain.AbilityEdges = 255; brain.AbilityEdgesTick = 1; f.Em.SetComponentData(root, brain);
            }
            f.Tick(); float3 center = pose.TransformPoint(new float3(4, 1, 6));
            foreach (var root in roots)
            {
                var cmd = f.Command(root); Assert.AreEqual(float2.zero, cmd.Thrust); Assert.AreEqual(0, cmd.Turn);
                Assert.AreEqual(0, cmd.FireGroups); Assert.AreEqual(ShipCommandFlags.None, cmd.Flags);
                Assert.AreEqual(ShipCommandEdges.Ability1 | ShipCommandEdges.Ability2 | ShipCommandEdges.Ability3 | ShipCommandEdges.Ability4, cmd.Edges);
                Assert.AreEqual(1, cmd.AimValid); Assert.AreEqual(center, cmd.AimWorld);
                Assert.AreEqual(1, f.Em.GetComponentData<WeaponControl>(root).Autofire, "Steering must not replace decision-owned autofire.");
            }
            f.Em.AddComponent<Hulk>(target); f.Tick();
            foreach (var root in roots) { Assert.AreEqual(0, f.Command(root).AimValid); Assert.AreEqual(float3.zero, f.Command(root).AimWorld); Assert.AreEqual(0, (ushort)f.Command(root).Edges); }
            f.Em.DestroyEntity(target); f.Tick(); foreach (var root in roots) Assert.AreEqual(0, f.Command(root).AimValid);
            f.Em.RemoveComponent<ShipTarget>(incomplete); f.Tick(); Assert.AreEqual(default(ShipCommand), f.Command(incomplete));
            f.Em.AddComponent<GridSpawning>(turret); f.Em.SetComponentData(turret, Stale);
            f.Tick(); Assert.AreEqual(default(ShipCommand), f.Command(turret), "Pending spawn commands are cleared without thrust.");
        }

        sealed class Fixture : IDisposable
        {
            public const float Arrival = 40;
            readonly EcsTestWorld world;
            readonly BlobAssetReference<AiProfilesBlob> profiles;
            readonly FactionRelations factions;
            readonly VoxelTestContent content;
            public EntityManager Em => world.EntityManager;
            public Fixture(bool decisions = false, bool physics = false)
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var blob = ref builder.ConstructRoot<AiProfilesBlob>(); var rows = builder.Allocate(ref blob.Profiles, 2);
                for (int i = 0; i < rows.Length; i++)
                {
                    rows[i] = new AiProfile { Role = i == 0 ? AiRole.Combat : AiRole.Turret, ThinkIntervalTicks = 4,
                        SensorRange = 1000, FleeHullFraction = .25f, Throttle = .85f, MinEngageRange = 80,
                        PreferredRangeFraction = .7f, OrbitStepRad = .35f };
                    var rules = builder.Allocate(ref rows[i].AbilityRules, 4);
                    for (int k = 0; k < rules.Length; k++) rules[k] = new AiAbilityRuleDef { AbilityIndex = (ushort)(k + 1), Trigger = AiAbilityTrigger.Fleeing };
                }
                builder.Allocate(ref blob.Formations, 0); profiles = builder.CreateBlobAssetReference<AiProfilesBlob>(Allocator.Persistent);
                try
                {
                    var systems = new List<Type> { typeof(AiSteeringSystem) };
                    if (decisions) { systems.Add(typeof(AiDecisionSystem)); systems.Add(typeof(SpatialHashBuildSystem)); }
                    if (physics)
                    {
                        systems.Add(typeof(PropulsionStatsSystem)); systems.Add(typeof(PropulsionSystem)); systems.Add(typeof(PlanarConstraintSystem));
                        foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                            if (type.Namespace != null && (type.Namespace.StartsWith("Unity.Physics") || type.Namespace == "Unity.Transforms")) systems.Add(type);
                    }
                    world = EcsTestWorld.Create(systems.ToArray()); world.AddGameplayActive(); world.AddSimClock(1);
                    world.AddSingleton(FlightConfig.Default);
                    world.AddSingleton(new AiConfig { Profiles = profiles, ArrivalRadius = Arrival, HashCellSize = 128, AvoidanceMargin = 25, DormantThinkMultiplier = 6 });
                    if (decisions) { factions = FactionRelations.CreateDefault(Allocator.Persistent); world.AddSingleton(factions); }
                    if (physics)
                    {
                        content = new VoxelTestContent(); content.Service.CreateSingletons(Em);
                        var step = PhysicsStep.Default; step.Gravity = float3.zero; world.AddSingleton(step);
                    }
                }
                catch { Dispose(); throw; }
            }
            public Entity Grid(float3 position, byte faction)
            {
                var root = Em.CreateEntity(typeof(LocalTransform), typeof(VoxelGrid), typeof(Allegiance));
                Em.SetComponentData(root, LocalTransform.FromPosition(position));
                Em.SetComponentData(root, new VoxelGrid { CellSize = 1, CellMin = new int3(-1), CellMax = int3.zero, Kind = GridKind.Ship });
                Em.SetComponentData(root, new Allegiance { Faction = faction }); return root;
            }
            public Entity Ship(ushort profile = 0, bool flight = true)
            {
                var root = Grid(float3.zero, 0);
                Em.AddComponentData(root, new AiBrain { Initialized = 1, ProfileIndex = profile, OrbitSign = 1,
                    GoalPosition = new float3(0, 0, 500), GoalSpeedFraction = .5f });
                Em.AddComponentData(root, default(ShipCommand)); Em.AddComponentData(root, default(ShipTarget));
                Em.AddComponentData(root, default(WeaponControl)); Em.AddComponentData(root, new WeaponStats { MaxWeaponRange = 500 });
                Em.AddComponentData(root, new ShipStats { MassFraction = 1 }); Em.AddComponentData(root, new PersistentId(4));
                if (flight) Em.AddComponentData(root, new FlightState { ForwardCap = 100, ReverseCap = 40, StrafeCap = 50, YawRateCap = 2 });
                return root;
            }
            public Entity PhysicalShip()
            {
                var root = Ship(); Em.SetComponentData(root, default(FlightState));
                Em.SetComponentData(root, new VoxelGrid { CellSize = .5f, Revision = 1 });
                Em.SetComponentData(root, new ShipStats { Mass = 400, MassFraction = 1, FunctionalRevision = 1 });
                Em.AddComponentData(root, default(PropulsionStats)); Em.AddComponent<PlanarConstraint>(root);
                Em.AddComponent<LocalToWorld>(root); Em.AddSharedComponent(root, new PhysicsWorldIndex(0));
                Em.AddComponentData(root, new PhysicsMass { Transform = new RigidTransform(quaternion.identity, new float3(.25f)),
                    InverseMass = 1f / 400, InverseInertia = new float3(0, .01f, 0) });
                Em.AddComponentData(root, default(PhysicsVelocity)); Em.AddBuffer<FlightThruster>(root); Em.AddBuffer<BlockInstance>(root);
                // Actual module definitions/orientations, four force lines through the centre of mass; no injected velocity or position updates.
                for (int i = 0; i < 4; i++)
                {
                    var thruster = Em.CreateEntity(typeof(FunctionalBlock), typeof(ThrusterState), typeof(Powered));
                    Em.SetComponentData(thruster, new FunctionalBlock { Grid = root, InstanceIndex = i });
                    byte orientation = content.Service.Orientations.Value.AxisTurn(1, i);
                    float3 offset = GridSpaceMath.BlockDirToGrid(new float3(-1, 0, 0), ref content.Service.Orientations.Value, orientation);
                    Em.GetBuffer<BlockInstance>(root).Add(new BlockInstance { Block = content.Index("block_thruster_medium"),
                        Orient = orientation, Origin = (int3)math.round(offset), Functional = thruster });
                }
                return root;
            }
            public void Tick() => world.StepTicks(1);
            public ShipCommand Command(Entity root) => Em.GetComponentData<ShipCommand>(root);
            public AiBrain Brain(Entity root) => Em.GetComponentData<AiBrain>(root);
            public void Dispose()
            {
                world?.Dispose(); content?.Dispose(); if (profiles.IsCreated) profiles.Dispose();
                if (factions.Stance.IsCreated) factions.Dispose();
            }
        }
    }
}
