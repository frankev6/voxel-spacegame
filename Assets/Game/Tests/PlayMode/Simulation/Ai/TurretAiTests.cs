using System;
using System.Collections.Generic;
using Game.App.Boot;
using Game.App.Systems;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Simulation.Ai
{
    /// <summary>AI-009: actual Station profile conversion, turret decisions and powered projectile firing.</summary>
    [Category("Scenario")]
    public sealed class TurretAiTests : GameTestBase
    {
        [Test] public void Station_TargetsAndFires()
        {
            using var f = new Fixture(); f.Ready(); f.Approach();
            bool acquired = false, fired = false;
            for (int frame = 0; frame < 230 && !fired; frame++)
            {
                uint tick = f.World.GetSingleton<SimClock>().Tick;
                bool inside = f.InSensor(); // Exactly the guarded previous-tick picture the decision job will read.
                bool think = (tick + (uint)Fixture.StationId) % (uint)f.Profile.ThinkIntervalTicks == 0;
                f.Tick(); var brain = f.Em.GetComponentData<AiBrain>(f.Station);
                var target = f.Em.GetComponentData<ShipTarget>(f.Station);
                if (!acquired && inside && think)
                {
                    Assert.AreEqual(f.Intruder, target.Value, "The first eligible think inside the sensor must acquire the approaching hostile.");
                    Assert.AreEqual(tick, brain.LastThinkTick); acquired = true;
                }
                if (!inside && !acquired) Assert.AreEqual(Entity.Null, target.Value);
                var command = f.Em.GetComponentData<ShipCommand>(f.Station);
                Assert.AreEqual(0, command.FireGroups, "Station firing must come from AI autofire, never held fire input.");
                if (f.Projectiles.Stats.Value.Fired == 0) continue;
                Assert.IsTrue(acquired); Assert.AreEqual(AiState.Hold, brain.State);
                var control = f.Em.GetComponentData<WeaponControl>(f.Station);
                Assert.AreEqual(1, control.Autofire); Assert.AreEqual(WeaponAimMode.TargetLead, control.AimMode);
                var aim = f.Em.GetComponentData<WeaponAim>(f.Mount);
                Assert.AreEqual(1, aim.InRange); Assert.AreEqual(1, aim.Aligned);
                Assert.IsTrue(f.Em.IsComponentEnabled<Powered>(f.Mount));
                Assert.Greater(f.Em.GetComponentData<WeaponState>(f.Mount).ShotCounter, 0);
                Assert.Greater(f.Projectiles.Rows.Length, 0);
                foreach (var row in f.Projectiles.Rows)
                {
                    Assert.AreEqual(f.Station, row.Source); Assert.AreEqual(f.Station, row.Instigator);
                    Assert.AreEqual(Entity.Null, row.Target, "Authored plasma is unguided; the weapon's lead determines its release direction.");
                    Assert.Greater(math.length(row.Velocity), 0);
                    Assert.Greater(math.dot(math.normalize(row.Velocity), math.normalizesafe(
                        f.Em.GetComponentData<LocalTransform>(f.Intruder).Position - row.Position)), .99f);
                }
                Assert.Less(f.Em.GetComponentData<LocalTransform>(f.Intruder).Position.z, f.IntruderStart.z - 200,
                    "The target must physically close from outside the sensor into the weapon's lead-intercept range.");
                fired = true;
            }
            Assert.IsTrue(acquired); Assert.IsTrue(fired, "The authored 600m-range turret must release real projectiles at the approaching ship.");
            Assert.AreEqual(600, f.Em.GetComponentData<WeaponStats>(f.Station).MaxWeaponRange, .001f);
        }

        [Test] public void Station_NeverMoves()
        {
            using var f = new Fixture(new float3(14, 0, -23), .7f); f.Ready();
            var before = f.Em.GetComponentData<LocalTransform>(f.Station); f.Approach();
            for (int frame = 0; frame < 230; frame++)
            {
                f.Em.SetComponentData(f.Station, new ShipCommand { Thrust = new float2(1), Turn = 1,
                    Flags = ShipCommandFlags.Boost | ShipCommandFlags.Brake, FireGroups = uint.MaxValue });
                f.Tick(); var command = f.Em.GetComponentData<ShipCommand>(f.Station);
                Assert.AreEqual(float2.zero, command.Thrust); Assert.AreEqual(0, command.Turn);
                Assert.AreEqual(ShipCommandFlags.None, command.Flags); Assert.AreEqual(0, command.FireGroups);
                Assert.AreEqual(before, f.Em.GetComponentData<LocalTransform>(f.Station), "Static station position, rotation and scale must remain exact through targeting and firing.");
                Assert.IsFalse(f.Em.HasComponent<FlightState>(f.Station)); Assert.IsFalse(f.Em.HasComponent<PhysicsVelocity>(f.Station));
            }
            Assert.AreEqual(AiState.Hold, f.Em.GetComponentData<AiBrain>(f.Station).State);
            Assert.AreEqual(f.Intruder, f.Em.GetComponentData<ShipTarget>(f.Station).Value);
            Assert.Greater(f.Projectiles.Stats.Value.Fired, 0, "Station pose stability must be exercised while its real turret fires.");
        }

        sealed class Fixture : IDisposable
        {
            public const ulong StationId = 100;
            readonly ContentService content;
            readonly BlueprintLibrary library;
            readonly VoxelStructureRegistry registry;
            readonly List<VoxelStructure> sources = new List<VoxelStructure>();
            BlobAssetReference<AiProfilesBlob> profiles;
            BlobAssetReference<WeaponDefsBlob> weapons;
            FactionRelations factions;
            ProjectileStore projectiles;
            public EcsTestWorld World { get; private set; }
            public EntityManager Em => World.EntityManager;
            public Entity Station { get; }
            public Entity Intruder { get; }
            public Entity Mount { get; private set; }
            public float3 IntruderStart { get; }
            public AiProfileDefinition Profile { get; }
            readonly float3 approach;
            public ProjectileStore Projectiles { get { projectiles.CompleteAccess(); return projectiles; } }

            public Fixture(float3 position = default, float yaw = 0)
            {
                try
                {
                    content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
                    library = new BlueprintLibrary(content.Catalog, new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations });
                    registry = VoxelStructureRegistry.CreateRuntime(library);
                    var systems = new List<Type>
                    {
                        typeof(GridSpawnSystem), typeof(GridBodyInitSystem), typeof(GridSpawnBodyInitSystem), typeof(GridMassUpdateSystem),
                        typeof(ColliderSwapSystem), typeof(GridColliderCleanupSystem), typeof(DeferredDisposalSystem), typeof(GameplayEndCommandBufferSystem),
                        typeof(ThrusterInitSystem), typeof(PropulsionSystem), typeof(PlanarConstraintSystem),
                        typeof(AiAnchorShiftSystem), typeof(AiDecisionSystem), typeof(AiSteeringSystem), typeof(SpatialHashBuildSystem),
                        typeof(PowerRegistrationSystem), typeof(PowerInitSystem), typeof(WeaponPowerDemandSystem), typeof(PowerAllocationSystem),
                        typeof(ShipStatsAggregateSystem), typeof(WeaponInitSystem), typeof(WeaponStatsSystem), typeof(WeaponAimSystem), typeof(WeaponFireSystem),
                        typeof(ProjectileShotHandlerSystem), typeof(FxEventQueueCreateSystem), typeof(SfxEventQueueCreateSystem), typeof(SimClockAdvanceSystem)
                    };
                    foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                        if (type.Namespace != null && (type.Namespace.StartsWith("Unity.Physics") || type.Namespace == "Unity.Transforms")) systems.Add(type);
                    World = EcsTestWorld.Create(systems.ToArray()); World.AddGameplayActive(); World.AddSimClock(); content.CreateSingletons(Em);
                    World.AddSingleton(new VoxelStructureRegistryRef { Value = registry }); World.AddSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
                    var physics = content.Catalog.Get<PhysicsVoxelConfigDefinition>(new ContentId("config_physics"));
                    World.AddSingleton(PhysicsVoxelConfigFactory.Create(physics)); World.AddSingleton(PhysicsVoxelConfigFactory.CreatePhysicsStep(physics));
                    World.AddSingleton(FlightConfig.Default); World.AddSingleton(PowerTuning.Defaults); World.AddSingleton(default(OriginShiftRecord));
                    AiContent.CreateSingletons(Em, content.Catalog, new List<Entity>()); profiles = World.GetSingleton<AiConfig>().Profiles;
                    WeaponContent.CreateSingletons(Em, content.Catalog, new List<Entity>());
                    weapons = World.GetSingleton<WeaponDefsRef>().Value; factions = World.GetSingleton<FactionRelations>();
                    // The bounded fixture retains actual ProjectileShotHandlerSystem output; hit/destruction is WPN-012's scope.
                    projectiles = ProjectileStore.Create(128); World.AddSingleton(projectiles);
                    Profile = content.Catalog.Get<AiProfileDefinition>(new ContentId("ai_turret"));
                    Assert.AreEqual(AiRole.Turret, Profile.Role); Assert.AreEqual(1200, Profile.SensorRange); Assert.AreEqual(6, Profile.ThinkIntervalTicks);
                    quaternion rotation = quaternion.RotateY(yaw); approach = -math.rotate(rotation, new float3(0, 0, 240));
                    IntruderStart = position + math.rotate(rotation, new float3(0, 0, 1250));
                    Station = Spawn(BuildStation(), new RigidTransform(rotation, position), GridProfile.Station, GridMotion.Static, GridKind.Station, StationId);
                    // Only documented requester values are supplied; ShipCommand must come from the production Station registration.
                    Em.AddComponentData(Station, new AiBrain { ProfileIndex = AiSetup.ProfileIndexOf(content.Catalog, Profile.Id) });
                    Em.AddComponentData(Station, new Allegiance { Faction = 0 });
                    Intruder = Spawn(BuildIntruder(), new RigidTransform(rotation, IntruderStart), GridProfile.Dummy, GridMotion.Dynamic, GridKind.Ship, 101);
                    Em.AddComponentData(Intruder, new Allegiance { Faction = 1 });
                }
                catch { Dispose(); throw; }
            }
            VoxelContent Content => new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations };
            void Place(VoxelStructure source, string id, int3 position)
                => Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement
                    { Block = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId(id))), Origin = position }, Content), id);
            VoxelStructure BuildStation()
            {
                var source = new VoxelStructure(.5f); sources.Add(source);
                for (int z = -6; z <= 3; z++) for (int x = -8; x <= 8; x++) Place(source, "block_hull", new int3(x, 0, z));
                Place(source, "block_core_small", new int3(-7, 1, -5)); Place(source, "block_reactor_small", new int3(-3, 1, -5));
                Place(source, "block_capacitor_small", new int3(3, 1, -5)); Place(source, "weapon_plasma_turret_small", new int3(0, 1, 0));
                return source;
            }
            VoxelStructure BuildIntruder()
            {
                var source = new VoxelStructure(.5f); sources.Add(source);
                for (int z = 0; z < 5; z++) for (int y = 0; y < 3; y++) for (int x = -1; x <= 1; x++) Place(source, "block_hull", new int3(x, y, z));
                return source;
            }
            Entity Spawn(VoxelStructure source, RigidTransform pose, GridProfile profile, GridMotion motion, GridKind kind, ulong id)
                => GridSpawnApi.RequestSpawn(Em, registry, registry.Register(source), new GridSpawnParams
                { Pose = pose, Profile = profile, Motion = motion, Kind = kind, PersistentId = id, Flags = GridSpawnFlags.NoRender | GridSpawnFlags.Transient });
            public void Ready()
            {
                for (int frame = 0; frame < 30; frame++)
                {
                    Tick(); if (Em.IsComponentEnabled<GridSpawning>(Station) || Em.IsComponentEnabled<GridSpawning>(Intruder)
                        || !Em.HasComponent<PhysicsCollider>(Station) || !Em.HasComponent<PhysicsVelocity>(Intruder)) continue;
                    foreach (var instance in Em.GetBuffer<BlockInstance>(Station))
                        if (instance.Functional != Entity.Null && Em.HasComponent<WeaponMount>(instance.Functional)) Mount = instance.Functional;
                    if (Mount == Entity.Null || Em.GetComponentData<WeaponMount>(Mount).Initialized == 0 || Em.GetComponentData<AiBrain>(Station).Initialized == 0) continue;
                    Assert.IsTrue(Em.HasComponent<ShipCommand>(Station), "Station profile conversion must supply its command without a test-added component.");
                    Assert.IsFalse(Em.HasComponent<FlightState>(Station)); Assert.IsFalse(Em.HasBuffer<FlightThruster>(Station));
                    Assert.IsFalse(Em.HasComponent<PropulsionStats>(Station)); Assert.IsFalse(Em.HasComponent<PlanarConstraint>(Station));
                    Assert.IsFalse(Em.HasComponent<PhysicsVelocity>(Station)); Assert.IsFalse(Em.HasComponent<PhysicsMass>(Station));
                    using var registrations = Em.CreateEntityQuery(ComponentType.ReadOnly<RootComponentRegistration>());
                    int commands = 0;
                    foreach (var row in Em.GetBuffer<RootComponentRegistration>(registrations.GetSingletonEntity()))
                        if (row.Profile == GridProfile.Station && row.Type == ComponentType.ReadWrite<ShipCommand>()) commands++;
                    Assert.AreEqual(1, commands, "Exactly one owner registers Station commands."); return;
                }
                Assert.Fail("Small station/intruder fixtures did not finish spawn, body and owner initialization within 30 updates.");
            }
            public void Approach() => Em.SetComponentData(Intruder, new PhysicsVelocity { Linear = approach });
            public bool InSensor()
            {
                var hash = World.GetSingleton<SpatialHash>(); hash.CompleteAccess();
                int own = hash.FindIndex(Station), other = hash.FindIndex(Intruder);
                Assert.GreaterOrEqual(own, 0); Assert.GreaterOrEqual(other, 0);
                return math.distance(hash.Get(own).Center, hash.Get(other).Center) <= Profile.SensorRange;
            }
            public void Tick() => World.StepTicks(1);
            public void Dispose()
            {
                World?.Dispose(); World = null; if (projectiles.Rows.IsCreated) projectiles.Dispose();
                if (profiles.IsCreated) profiles.Dispose(); if (weapons.IsCreated) weapons.Dispose(); if (factions.Stance.IsCreated) factions.Dispose();
                if (registry != null) Object.DestroyImmediate(registry);
                foreach (var source in sources) source.Dispose(); sources.Clear(); library?.Dispose(); content?.Dispose();
            }
        }
    }
}
