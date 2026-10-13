using System;
using System.Collections.Generic;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Ai
{
    /// <summary>SPEC-23 AI-010: four bounded physical scenarios through production AI, propulsion, weapons and damage.</summary>
    [Category("Scenario")]
    public sealed class AiScenarioTests : GameTestBase
    {
        [Test] public void Engage_OrbitsAndHits()
        {
            using var f = new Fixture();
            var ship = f.Frigate(new float3(0, 0, -150)); var player = f.PlayerDummy(float3.zero);
            f.Ready(ship, player); float initial = f.Distance(ship, player);
            int pristine = f.Combat.Cells(player); float preferred = f.PreferredRange(ship), nearest = initial, angleTravel = 0;
            float lastAngle = f.Angle(ship, player); bool engaged = false, lateral = false;
            f.StartAi();
            for (int tick = 0; tick < 360; tick++)
            {
                f.Tick(); var brain = f.Brain(ship); float distance = f.Distance(ship, player), angle = f.Angle(ship, player);
                engaged |= brain.State == AiState.Engage; nearest = math.min(nearest, distance);
                if (brain.State == AiState.Engage && distance <= preferred + Fixture.Arrival + 10)
                {
                    angleTravel += math.abs(Game.Core.Planar.WrapAngle(angle - lastAngle));
                    float3 radial = math.normalizesafe(f.Centre(ship) - f.Centre(player));
                    lateral |= math.abs(math.cross(radial, f.Em.GetComponentData<PhysicsVelocity>(ship).Linear).y) > 1;
                }
                lastAngle = angle;
                if (nearest <= preferred + Fixture.Arrival && angleTravel > .12f && lateral && f.Damage(player).CellsDestroyed > 0) break;
            }
            Assert.IsTrue(engaged); Assert.AreEqual(player, f.Em.GetComponentData<ShipTarget>(ship).Value);
            Assert.Less(nearest, initial - 30, "Real thrusters must close the initial distance.");
            Assert.LessOrEqual(nearest, preferred + Fixture.Arrival, "Arrival tolerance surrounds the preferred orbit radius.");
            Assert.Greater(angleTravel, .12f, "The ship must travel around the target, rather than only point at it."); Assert.IsTrue(lateral);
            Assert.Greater(f.Em.GetComponentData<WeaponState>(f.Combat.Mount(ship)).ShotCounter, 0);
            Assert.Greater(f.Combat.Store.Stats.Value.Hits, 0); Assert.Greater(f.Damage(player).DamageTaken, 0);
            Assert.Less(f.Combat.Cells(player), pristine, "The production shots must remove cells, not only increment a harness counter.");
            Assert.AreEqual(0u, f.Em.GetComponentData<ShipCommand>(ship).FireGroups, "AI firing must use decision-owned autofire.");
            Assert.That(f.Em.GetComponentData<LocalTransform>(ship).Position.y, Is.EqualTo(0).Within(.001f));
        }

        [Test] public void Flee_BelowThreshold()
        {
            using var f = new Fixture(); var ship = f.Frigate(new float3(0, 0, -90)); var player = f.PlayerDummy(float3.zero);
            f.Ready(ship, player);
            // Seed an already-damaged starting condition, as the combat fixtures seed partial shield charge.
            // A preserved full-mass baseline keeps the production aggregate honest if mass is refreshed later.
            var stats = f.Em.GetComponentData<ShipStats>(ship); stats.InitialMass = stats.Mass / .1f; stats.MassFraction = .1f; f.Em.SetComponentData(ship, stats);
            float initial = f.Distance(ship, player); float3 start = f.Em.GetComponentData<LocalTransform>(ship).Position;
            bool fleeing = false; f.StartAi();
            for (int tick = 0; tick < 180; tick++)
            {
                f.Tick(); var brain = f.Brain(ship); if (brain.State != AiState.Flee) continue;
                fleeing = true; Assert.AreEqual(0, f.Em.GetComponentData<WeaponControl>(ship).Autofire);
                Assert.Greater(math.dot(brain.GoalPosition - f.Em.GetComponentData<LocalTransform>(ship).Position,
                    f.Centre(ship) - f.Centre(player)), 0, "The flee goal must point away from the hostile.");
                if (f.Distance(ship, player) > initial + 15 &&
                    math.dot(f.Em.GetComponentData<PhysicsVelocity>(ship).Linear, f.Centre(ship) - f.Centre(player)) > 0) break;
            }
            Assert.IsTrue(fleeing); Assert.AreEqual(AiState.Flee, f.Brain(ship).State);
            Assert.Less(f.Em.GetComponentData<ShipStats>(ship).MassFraction, .25f);
            Assert.Greater(f.Distance(ship, player), initial + 15, "Flee must produce physical separation.");
            Assert.Greater(math.distance(start, f.Em.GetComponentData<LocalTransform>(ship).Position), 15);
            Assert.Greater(math.dot(f.Em.GetComponentData<PhysicsVelocity>(ship).Linear, f.Centre(ship) - f.Centre(player)), 0);
            Assert.AreEqual(0u, f.Em.GetComponentData<WeaponState>(f.Combat.Mount(ship)).ShotCounter);
        }

        [Test] public void Allies_NoFriendlyFire()
        {
            using var f = new Fixture(); var rear = f.Frigate(new float3(0, 0, -90)); var front = f.Frigate(new float3(0, 0, -45));
            var player = f.PlayerDummy(float3.zero); f.Ready(rear, front, player); f.Combat.Freeze(rear); f.Combat.Freeze(front);
            ulong allyBefore = CellStateHasher.ComputeGridHash(f.Em, front); f.StartAi();
            for (int tick = 0; tick < 40; tick++)
            {
                f.Tick(); if (f.Brain(rear).State != AiState.Engage) continue;
                Assert.AreEqual(player, f.Em.GetComponentData<ShipTarget>(rear).Value); Assert.AreEqual(1, f.Brain(rear).FireBlocked);
                Assert.AreEqual(0, f.Em.GetComponentData<WeaponControl>(rear).Autofire);
                Assert.AreEqual(0u, f.Em.GetComponentData<WeaponState>(f.Combat.Mount(rear)).ShotCounter);
            }
            Assert.AreEqual(AiState.Engage, f.Brain(rear).State);
            Assert.Greater(f.Em.GetComponentData<WeaponState>(f.Combat.Mount(front)).ShotCounter, 0, "The unobstructed allied ship must still fire.");
            Assert.AreEqual(allyBefore, CellStateHasher.ComputeGridHash(f.Em, front)); Assert.AreEqual(0, f.Damage(front).DamageTaken);
            // One between-tick fixture relocation clears the obstruction; neither firing nor decisions are injected.
            var pose = f.Em.GetComponentData<LocalTransform>(front); pose.Position.x = 80; f.Em.SetComponentData(front, pose);
            for (int tick = 0; tick < 20; tick++) f.Tick();
            Assert.AreEqual(0, f.Brain(rear).FireBlocked); Assert.AreEqual(1, f.Em.GetComponentData<WeaponControl>(rear).Autofire);
            Assert.Greater(f.Em.GetComponentData<WeaponState>(f.Combat.Mount(rear)).ShotCounter, 0, "The production path must resume when the line clears.");
            Assert.AreEqual(allyBefore, CellStateHasher.ComputeGridHash(f.Em, front));
        }

        [Test] public void Leash_Returns()
        {
            using var f = new Fixture(leash: 32); var ship = f.Frigate(float3.zero); var player = f.PlayerDummy(new float3(0, 0, 150));
            f.Ready(ship, player); float3 anchor = f.Brain(ship).Anchor; float peak = 0; bool engaged = false, returned = false, movedBack = false;
            f.StartAi();
            for (int tick = 0; tick < 540; tick++)
            {
                f.Tick(); var brain = f.Brain(ship); float distance = math.distance(anchor, f.Em.GetComponentData<LocalTransform>(ship).Position);
                engaged |= brain.State == AiState.Engage; peak = math.max(peak, distance);
                if (brain.State == AiState.Return)
                {
                    returned = true; Assert.AreEqual(Entity.Null, f.Em.GetComponentData<ShipTarget>(ship).Value);
                    Assert.AreEqual(0, f.Em.GetComponentData<WeaponControl>(ship).Autofire); Assert.AreEqual(anchor, brain.GoalPosition);
                }
                if (returned && distance < peak - 10) movedBack = true;
                if (returned && movedBack && distance < Fixture.Arrival * 2) break;
            }
            Assert.IsTrue(engaged); Assert.Greater(peak, 48, "The actor must chase beyond 1.5 times its test leash through real propulsion.");
            Assert.IsTrue(returned, $"The actor must enter Return after crossing its leash; peak={peak}, state={f.Brain(ship).State}.");
            Assert.IsTrue(movedBack, $"The actor must physically reverse its chase; peak={peak}, final distance={math.distance(anchor, f.Em.GetComponentData<LocalTransform>(ship).Position)}.");
            Assert.Less(math.distance(anchor, f.Em.GetComponentData<LocalTransform>(ship).Position), Fixture.Arrival * 2,
                "Return remains latched until the actor physically reaches the specified anchor-release radius.");
            Assert.AreEqual(anchor, f.Brain(ship).Anchor, "The chase must not move the spawn anchor.");
        }

        sealed class Fixture : IDisposable
        {
            public const float Arrival = 20;
            public readonly CombatFixture Combat;
            readonly BlobAssetReference<AiProfilesBlob> profiles;
            readonly BlobAssetReference<WeaponDefsBlob> fastWeapons;
            readonly AiConfig config;
            public EntityManager Em => Combat.Em;
            public Fixture(float leash = 1200)
            {
                Combat = new CombatFixture(exactAim: true);
                try
                {
                    // Fire timing/damage balance is not under test: finish hit/clearance assertions immediately.
                    using (var weaponQuery = Em.CreateEntityQuery(typeof(WeaponDefsRef)))
                    using (var builder = new BlobBuilder(Allocator.Temp))
                    {
                        var authored = weaponQuery.GetSingleton<WeaponDefsRef>().Value;
                        ref var blob = ref builder.ConstructRoot<WeaponDefsBlob>();
                        var rows = builder.Allocate(ref blob.Weapons, authored.Value.Weapons.Length);
                        for (int i = 0; i < rows.Length; i++)
                        {
                            var row = authored.Value.Weapons[i];
                            if (i != 0)
                            {
                                row.Damage *= 10; row.TriggerIntervalTicks = 6; row.BurstCount = 1; row.BurstIntervalTicks = 0;
                                row.SustainedDps = WeaponContent.ComputeSustainedDps(row);
                            }
                            rows[i] = row;
                        }
                        fastWeapons = builder.CreateBlobAssetReference<WeaponDefsBlob>(Allocator.Persistent);
                        Em.SetComponentData(weaponQuery.GetSingletonEntity(), new WeaponDefsRef { Value = fastWeapons });
                    }
                    DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(Combat.World.World, new[] {
                        typeof(AiAnchorShiftSystem), typeof(AiDecisionSystem), typeof(AiSteeringSystem),
                        typeof(PlayerCombatStatusSystem), typeof(PropulsionStatsSystem) });
                    Combat.World.World.GetExistingSystemManaged<ShipCommandGroup>().SortSystems();
                    Em.CreateSingleton(default(OriginShiftRecord));
                    var created = new List<Entity>(); AiContent.CreateSingletons(Em, Combat.Flight.Catalog, created);
                    using var query = Em.CreateEntityQuery(typeof(AiConfig)); config = query.GetSingleton<AiConfig>(); Em.DestroyEntity(query.GetSingletonEntity());
                    using (var authored = config.Profiles)
                    using (var builder = new BlobBuilder(Allocator.Temp))
                    {
                        ref var blob = ref builder.ConstructRoot<AiProfilesBlob>(); var rows = builder.Allocate(ref blob.Profiles, 1);
                        rows[0] = authored.Value.Profiles[0]; rows[0].AbilityRules = default; builder.Allocate(ref rows[0].AbilityRules, 0);
                        // Keep steering laws; arena distances and the seeded thrust modifier bound fixture travel.
                        rows[0].PreferredRangeFraction = .1f; rows[0].LeashRadius = leash; rows[0].PatrolRadius = 0;
                        builder.Allocate(ref blob.Formations, 0); profiles = builder.CreateBlobAssetReference<AiProfilesBlob>(Allocator.Persistent);
                    }
                    config.Profiles = profiles; config.ArrivalRadius = Arrival;
                }
                catch { Dispose(); throw; }
            }
            public Entity Frigate(float3 position)
            {
                var source = new VoxelStructure(.5f);
                for (int z = -5; z <= 3; z++) for (int x = -4; x <= 4; x++) Place("block_hull", new int3(x, 0, z));
                Place("block_core_small", new int3(-4, 1, -4)); Place("block_reactor_small", new int3(-2, 1, -4));
                Place("block_capacitor_small", new int3(1, 1, -4)); Place("weapon_plasma_turret_medium", new int3(0, 1, 0));
                var origins = new[] { new int3(4, 1, 1), new int3(-1, 1, -1), new int3(-4, 1, 1), new int3(1, 1, -2) };
                for (int i = 0; i < 4; i++) Place("block_thruster_small", origins[i], Combat.Flight.Content.Orientations.Value.AxisTurn(1, i));
                var root = Combat.Flight.SpawnSource(source, position, profile: GridProfile.NpcShip);
                Em.AddComponentData(root, new Allegiance { Faction = 1 }); return root;
                void Place(string id, int3 origin, byte orientation = 0) => Assert.AreEqual(PlaceResult.Ok,
                    source.TryPlaceBlock(new BlockPlacement { Block = Combat.Flight.Index(id), Origin = origin, Orient = orientation }, Combat.Flight.Content), id);
            }
            public Entity PlayerDummy(float3 position)
            {
                var root = Combat.Wall(position, halfWidth: 6, depth: 2); Em.AddComponent<PlayerShip>(root);
                Em.AddComponentData(root, Allegiance.Player); return root;
            }
            public void Ready(params Entity[] roots)
            {
                Combat.Ready(roots);
                foreach (var root in roots) if (Em.HasComponent<PlayerShip>(root)) Combat.Freeze(root);
                foreach (var root in roots) if (Em.HasComponent<AiBrain>(root))
                {
                    Assert.AreEqual(1, Brain(root).Initialized); Assert.Greater(Em.GetBuffer<Game.Simulation.Flight.FlightThruster>(root).Length, 0);
                    Assert.Greater(Em.GetComponentData<WeaponStats>(root).MaxWeaponRange, 0);
                    // Seed fixture engine capacity through the same aggregate consumed by both steering and propulsion.
                    // Do not replace velocities, thruster forces or acceleration stats to manufacture movement.
                    Em.GetBuffer<ShipStatModifier>(root).Add(new ShipStatModifier {
                        Stat = StatId.Thrust, Producer = ProducerKind.Test, Multiplier = 4, UntilTick = uint.MaxValue });
                }
            }
            public void StartAi() => Em.CreateSingleton(config);
            public void Tick() => Combat.Tick();
            public AiBrain Brain(Entity root) => Em.GetComponentData<AiBrain>(root);
            public GridDamageStats Damage(Entity root) => Em.GetComponentData<GridDamageStats>(root);
            public float3 Centre(Entity root)
            {
                var grid = Em.GetComponentData<VoxelGrid>(root);
                return Em.GetComponentData<LocalTransform>(root).TransformPoint(((float3)grid.CellMin + (float3)(grid.CellMax + 1)) * (.5f * grid.CellSize));
            }
            public float Distance(Entity a, Entity b) => math.distance(Centre(a), Centre(b));
            public float Angle(Entity a, Entity b) { var delta = Centre(a) - Centre(b); return math.atan2(delta.x, delta.z); }
            public float PreferredRange(Entity root) => math.max(80, Em.GetComponentData<WeaponStats>(root).MaxWeaponRange * .1f);
            public void Dispose()
            { Combat?.Dispose(); if (profiles.IsCreated) profiles.Dispose(); if (fastWeapons.IsCreated) fastWeapons.Dispose(); }
        }
    }
}
