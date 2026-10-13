using System;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using FlightFixture = Game.Tests.PlayMode.Simulation.Flight.FlightScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Weapons
{
    /// <summary>WPN-012: production fire-to-destruction scenarios, with test-only structures and authored block definitions.</summary>
    [Category("Scenario")]
    public partial class CombatScenarioTests
    {
        internal sealed class Fixture : IDisposable
        {
            public readonly FlightFixture Flight;
            readonly BlobAssetReference<WeaponDefsBlob> weapons;
            readonly FactionRelations factions;
            public EntityManager Em => Flight.Em;
            public EcsTestWorld World => Flight.Test;
            public Fixture()
            {
                Flight = new FlightFixture(typeof(WeaponInitSystem), typeof(PowerRegistrationSystem),
                    typeof(PowerInitSystem), typeof(WeaponPowerDemandSystem), typeof(ShieldPowerDemandSystem),
                    typeof(PowerAllocationSystem), typeof(ShipStatsAggregateSystem), typeof(WeaponStatsSystem),
                    typeof(WeaponAimSystem), typeof(WeaponFireSystem), typeof(ProjectileShotHandlerSystem),
                    typeof(BeamShotHandlerSystem), typeof(ProjectileStepSystem), typeof(BeamStepSystem),
                    typeof(ExplosionSystem), typeof(ShieldSystem), typeof(SpatialHashBuildSystem),
                    typeof(DamageStreamsSystem), typeof(ImpactEventSystem), typeof(DamageResolveSystem),
                    typeof(IntegritySystem), typeof(SplitSystem), typeof(ShipDeathSystem),
                    typeof(ChunkColliderCleanupSystem), typeof(SplitGraceContactSystem),
                    typeof(FxEventQueueCreateSystem), typeof(SfxEventQueueCreateSystem));
                weapons = WeaponContent.BuildBlob(Flight.Catalog);
                factions = FactionRelations.CreateDefault(Allocator.Persistent);
                Em.CreateSingleton(new WeaponDefsRef { Value = weapons }); Em.CreateSingleton(factions);
                Em.CreateSingleton(new WeaponTuning { MaxProjectiles = 8192, MaxCandidates = 32,
                    FixedConvergenceRad = math.radians(3), ExplosionImpulsePerDamage = 2, MaxTargetRange = 2500, UnderCursorRadius = 30 });
                Em.CreateSingleton(new PersistentIdAllocator { Next = 1000, End = 10000 });
            }
            void Place(VoxelStructure source, string id, int3 p)
                => Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = Flight.Index(id), Origin = p }, Flight.Content), id);

            public Entity Ship(string weapon, float3 position, bool shield = false)
            {
                var source = new VoxelStructure(.5f);
                for (int z = -6; z <= 3; z++) for (int x = -8; x <= 8; x++) Place(source, "block_hull", new int3(x, 0, z));
                Place(source, "block_core_small", new int3(-7,1,-5));
                Place(source, "block_reactor_small", new int3(-3,1,-5));
                Place(source, "block_capacitor_small", new int3(3,1,-5));
                Place(source, weapon, new int3(0,1,0));
                if (shield) Place(source, "block_shield_small", new int3(5,1,0));
                return Flight.SpawnSource(source, position);
            }
            public Entity Wall(float3 position, int halfWidth = 6, int depth = 4)
            {
                var source = new VoxelStructure(.5f);
                for (int z = 0; z < depth; z++) for (int y = 0; y <= 6; y++) for (int x = -halfWidth; x <= halfWidth; x++)
                    Place(source, "block_hull", new int3(x,y,z));
                return Flight.SpawnSource(source, position);
            }
            public void Ready(params Entity[] roots)
            {
                Flight.Ready(roots);
                // Fix preparation at the same tick across worker counts before scripted combat starts.
                Assert.Less(World.TicksRun, 120u);
                while (World.TicksRun < 120) Flight.Tick();
                for (int i = 0; i < roots.Length; i++)
                {
                    Em.SetComponentData(roots[i], new PersistentId { Value = (ulong)(100 + i) });
                    Em.SetComponentData(roots[i], default(PhysicsVelocity));
                }
            }
            public Entity Mount(Entity root)
            {
                foreach (var instance in Em.GetBuffer<BlockInstance>(root))
                    if (instance.Functional != Entity.Null && Em.HasComponent<WeaponMount>(instance.Functional)) return instance.Functional;
                Assert.Fail("Spawned weapon block must have its production mount."); return Entity.Null;
            }
            public float3 Muzzle(Entity root)
            {
                var mount = Em.GetComponentData<WeaponMount>(Mount(root));
                return Em.GetComponentData<LocalTransform>(root).TransformPoint(mount.MuzzleLocal);
            }
            public void Fire(Entity root, float3 aim, Entity target = default)
            {
                Em.SetComponentData(root, new ShipTarget { Value = target });
                var state = Em.GetComponentData<WeaponState>(Mount(root));
                Em.SetComponentData(root, new ShipCommand { AimValid = 1, AimWorld = aim, FireGroups = state.Group });
            }
            public void Stop(Entity root) => Em.SetComponentData(root, default(ShipCommand));
            public void Tick(int count = 1) => Flight.Tick(count);
            public ProjectileStore Store { get { var store = World.GetSingleton<ProjectileStore>(); store.CompleteAccess(); return store; } }
            public int Cells(Entity root)
            {
                World.CompleteAll(); int n = 0;
                foreach (var slot in Em.GetBuffer<BrickSlot>(root))
                    if (slot.Brick != Entity.Null && Em.Exists(slot.Brick)) n += Em.GetComponentData<Brick>(slot.Brick).SolidCount;
                return n;
            }
            public void Dispose() { Flight.Dispose(); weapons.Dispose(); factions.Dispose(); }
        }
    }
}
