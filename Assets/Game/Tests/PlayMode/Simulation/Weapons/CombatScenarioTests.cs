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
    public unsafe partial class CombatScenarioTests
    {
        internal sealed class Fixture : IDisposable
        {
            public readonly FlightFixture Flight;
            readonly BlobAssetReference<WeaponDefsBlob> weapons;
            readonly FactionRelations factions;
            public EntityManager Em => Flight.Em;
            public EcsTestWorld World => Flight.Test;
            public Fixture(bool exactAim = false)
            {
                Flight = new FlightFixture(typeof(WeaponInitSystem), typeof(PowerRegistrationSystem),
                    typeof(PowerInitSystem), typeof(WeaponPowerDemandSystem), typeof(ShieldPowerDemandSystem),
                    typeof(PowerAllocationSystem), typeof(ShipStatsAggregateSystem), typeof(WeaponStatsSystem),
                    typeof(WeaponAimSystem), typeof(WeaponFireSystem), typeof(ProjectileShotHandlerSystem),
                    typeof(BeamShotHandlerSystem), typeof(ProjectileStepSystem), typeof(BeamStepSystem),
                    typeof(ExplosionSystem), typeof(ShieldSystem), typeof(SpatialHashBuildSystem),
                    typeof(DamageStreamsSystem), typeof(ImpactEventSystem), typeof(DamageResolveSystem),
                    typeof(IntegritySystem), typeof(DebrisEmitSystem), typeof(SplitSystem), typeof(ShipDeathSystem), typeof(StructuralDestroySystem),
                    typeof(ChunkColliderCleanupSystem), typeof(SplitGraceContactSystem),
                    typeof(FxEventQueueCreateSystem), typeof(SfxEventQueueCreateSystem));
                using (var authored = WeaponContent.BuildBlob(Flight.Catalog))
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var blob = ref builder.ConstructRoot<WeaponDefsBlob>();
                    var rows = builder.Allocate(ref blob.Weapons, authored.Value.Weapons.Length);
                    for (int i = 0; i < rows.Length; i++)
                    { var row = authored.Value.Weapons[i]; if (exactAim) row.SpreadRad = 0; rows[i] = row; }
                    weapons = builder.CreateBlobAssetReference<WeaponDefsBlob>(Allocator.Persistent);
                }
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
                if (Em.HasComponent<PlayerShip>(root))
                    Em.SetComponentData(Flight.InputEntity,new PlayerCommand { AimValid = 1, AimWorld = aim, FireGroups = state.Group });
            }
            public void Freeze(Entity root)
            {
                var mass = Em.GetComponentData<PhysicsMass>(root); mass.InverseMass = 0; mass.InverseInertia = 0;
                Em.SetComponentData(root, mass); Em.SetComponentData(root, default(PhysicsVelocity));
                Em.AddComponentData(root,new PhysicsMassOverride { IsKinematic = 1, SetVelocityToZero = 1 });
            }
            public void Stop(Entity root)
            {
                Em.SetComponentData(root, default(ShipCommand));
                if (Em.HasComponent<PlayerShip>(root)) Em.SetComponentData(Flight.InputEntity,default(PlayerCommand));
            }
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

        [Test] public void Plasma_HolesAlongLine()
        {
            using var f = new Fixture(exactAim: true);
            var player = f.Ship("weapon_plasma_turret_small", default);
            var target = f.Wall(new float3(0,0,15)); f.Ready(player,target);
            f.Freeze(player); f.Freeze(target); f.Em.AddComponent<Hulk>(target); f.Em.AddComponent<PlayerShip>(player);
            var muzzle = f.Muzzle(player); f.Fire(player, muzzle + new float3(0,0,100));
            int removed = 0, before = f.Cells(target);
            int2 line = (int2)math.floor(muzzle.xy / .5f);
            for (int tick = 0; tick < 240; tick++)
            {
                f.Tick(); var streams = f.World.GetSingleton<DamageStreams>(); streams.Data->AllJobs(default).Complete();
                foreach (var cell in streams.Data->Removed)
                    if (cell.Grid == target && cell.Reason == CellRemovedReason.Destroyed)
                    { Assert.AreEqual(line.x, cell.Cell.x); Assert.AreEqual(line.y, cell.Cell.y); removed++; }
            }
            Assert.Greater(removed,0,"Real plasma must remove hull cells on its shot line.");
            Assert.Less(f.Cells(target),before); Assert.Greater(f.Store.Stats.Value.Hits,0);
        }

        [Test] public void Shield_AbsorbThenOverflow()
        {
            using var f = new Fixture(exactAim: true);
            var player = f.Ship("weapon_plasma_turret_small",default);
            var target = f.Ship("weapon_plasma_turret_small",new float3(0,0,20),shield:true);
            f.Ready(player,target); f.Freeze(player); f.Freeze(target);
            var shield = f.Em.GetComponentData<ShieldState>(target);
            Assert.AreEqual(1,shield.Active); Assert.Greater(shield.MaxHp,100);
            // Seed a partially charged shield; all subsequent HP and hull changes come from the production pipeline.
            shield.Hp = 90; f.Em.SetComponentData(target,shield);
            ulong pristine = CellStateHasher.ComputeGridHash(f.Em,target);
            f.Fire(player,new float3(0.25f,0.25f,20)); bool absorbed = false, overflow = false;
            for (int tick = 0; tick < 240 && !overflow; tick++)
            {
                f.Tick(); shield = f.Em.GetComponentData<ShieldState>(target);
                ulong hull = CellStateHasher.ComputeGridHash(f.Em,target);
                if (shield.Hp > 0 && shield.Hp < 90)
                { absorbed = true; Assert.AreEqual(pristine,hull,"Shield absorption must leave cells unchanged."); }
                if (shield.Hp == 0 && hull != pristine) overflow = true;
            }
            Assert.IsTrue(absorbed); Assert.IsTrue(overflow,"A later shot must exhaust the shield and damage the hull.");
        }

        [Test] public void Flak_TwoShips()
        {
            using var f = new Fixture(exactAim:true);
            var player = f.Ship("weapon_flak_cannon_medium",default);
            var left = f.Ship("weapon_plasma_turret_small",new float3(-4.5f,0,20));
            var right = f.Ship("weapon_plasma_turret_small",new float3(4.5f,0,20));
            f.Ready(player,left,right); f.Freeze(player); f.Freeze(left); f.Freeze(right);
            ulong a = CellStateHasher.ComputeGridHash(f.Em,left), b = CellStateHasher.ComputeGridHash(f.Em,right);
            f.Fire(player,new float3(-.25f,.25f,17));
            f.Tick(); f.Stop(player); f.Tick(100);
            Assert.AreEqual(8,f.Store.Stats.Value.Fired,"One authored flak burst releases eight pellets.");
            Assert.AreNotEqual(a,CellStateHasher.ComputeGridHash(f.Em,left));
            Assert.AreNotEqual(b,CellStateHasher.ComputeGridHash(f.Em,right),"The same flak burst must damage the adjacent ship.");
        }

        [Test] public void Missiles_Track()
        {
            using var f = new Fixture(exactAim:true);
            var player = f.Ship("weapon_missile_launcher_small",default);
            var target = f.Ship("weapon_plasma_turret_small",new float3(0,0,100));
            f.Ready(player,target); f.Freeze(player);
            f.Em.SetComponentData(target,new PhysicsVelocity { Linear = new float3(8,0,0) });
            float initialX = f.Em.GetComponentData<LocalTransform>(target).Position.x;
            ulong pristine = CellStateHasher.ComputeGridHash(f.Em,target);
            f.Fire(player,new float3(0,.5f,100),target); f.Tick(); f.Stop(player);
            bool tracked = false; float firstX = f.Store.Rows.Length > 0 ? f.Store.Rows[0].Velocity.x : 0;
            for (int tick = 0; tick < 120; tick++)
            {
                f.Tick(); var store = f.Store;
                foreach (var row in store.Rows)
                    if (row.Target == target && row.Velocity.x > firstX + .1f) tracked = true;
            }
            Assert.Greater(f.Em.GetComponentData<LocalTransform>(target).Position.x,initialX + .5f);
            Assert.IsTrue(tracked,"A released missile must turn toward the later sideways motion.");
            Assert.AreNotEqual(pristine,CellStateHasher.ComputeGridHash(f.Em,target),"The homing missile must reach and damage its moving target.");
        }

        [Test] public void Beam_StopsAtTarget()
        {
            using var f = new Fixture();
            var player = f.Ship("weapon_small_beam_turret",default);
            var front = f.Wall(new float3(0,0,15),depth:8); var behind = f.Wall(new float3(0,0,25));
            f.Ready(player,front,behind); f.Freeze(player); f.Freeze(front); f.Freeze(behind);
            ulong untouched = CellStateHasher.ComputeGridHash(f.Em,behind), hit = CellStateHasher.ComputeGridHash(f.Em,front);
            var muzzle = f.Muzzle(player); f.Fire(player,muzzle + new float3(0,0,100)); bool clipped = false;
            for (int tick = 0; tick < 30; tick++)
            {
                f.Tick(); var beam = f.Em.GetComponentData<BeamState>(f.Mount(player));
                if (beam.HitSomething != 0) { clipped = true; Assert.That(beam.EndWorld.z,Is.InRange(15f,19.01f)); }
            }
            Assert.IsTrue(clipped); Assert.AreNotEqual(hit,CellStateHasher.ComputeGridHash(f.Em,front));
            Assert.AreEqual(untouched,CellStateHasher.ComputeGridHash(f.Em,behind),"The rear hull must receive no beam damage.");
        }
    }
}
