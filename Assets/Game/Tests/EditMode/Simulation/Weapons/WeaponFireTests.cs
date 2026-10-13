using System;
using System.Collections.Generic;
using Game.Core;
using Game.App.Systems;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    public sealed class WeaponFireTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly EcsTestWorld World;
            public readonly Entity Root, Mount;
            public readonly ProjectileStore Store;
            readonly BlobAssetReference<WeaponDefsBlob> definitions;
            readonly FactionRelations relations;
            public EntityManager Em => World.EntityManager;
            public WeaponState State => Em.GetComponentData<WeaponState>(Mount);
            public Fixture(WeaponDef weapon, int capacity = 8192)
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var blob = ref builder.ConstructRoot<WeaponDefsBlob>();
                var rows = builder.Allocate(ref blob.Weapons, 4); rows[1] = weapon;
                rows[2] = Definition(); var beam = Definition(); beam.Delivery = WeaponDelivery.Beam; rows[3] = beam;
                definitions = builder.CreateBlobAssetReference<WeaponDefsBlob>(Allocator.Persistent);
                World = EcsTestWorld.Create(typeof(WeaponFireSystem), typeof(ProjectileShotHandlerSystem), typeof(BeamShotHandlerSystem), typeof(BeamStepSystem),
                    typeof(FxEventQueueCreateSystem), typeof(SfxEventQueueCreateSystem), typeof(FireCaptureSystem), typeof(SimClockAdvanceSystem));
                World.AddSingleton(new WeaponDefsRef { Value = definitions });
                World.AddSingleton(new WeaponTuning { MaxProjectiles = capacity });
                Store = ProjectileStore.Create(capacity); World.AddSingleton(Store);
                relations = FactionRelations.CreateDefault(Allocator.Persistent); World.AddSingleton(relations);
                World.AddGameplayActive(); World.AddSimClock();
                Root = Em.CreateEntity(typeof(LocalTransform), typeof(PersistentId), typeof(ShipCommand), typeof(WeaponControl), typeof(ShipTarget));
                Em.SetComponentData(Root, LocalTransform.Identity); Em.SetComponentData(Root, new PersistentId(77));
                Em.SetComponentData(Root, new ShipCommand { AimValid = 1, FireGroups = 1 });
                Mount = AddMount(0);
            }
            public Entity AddMount(int index, ushort definition = 1)
            {
                var entity = Em.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount), typeof(WeaponAim), typeof(WeaponState), typeof(BeamState));
                Em.SetComponentData(entity, new FunctionalBlock { Grid = Root, InstanceIndex = index });
                Em.SetComponentData(entity, new WeaponMount { Initialized = 1, WeaponIndex = definition, ForwardLocal = new float3(0,0,1), UpLocal = new float3(0,1,0) });
                Em.SetComponentData(entity, new WeaponAim { HasAim = 1, Aligned = 1, InRange = 1, FireDirLocal = new float3(0,0,1) });
                Em.SetComponentData(entity, new WeaponState { Group = 1 });
                return entity;
            }
            public void Step(int ticks = 1) { World.StepTicks(ticks); Em.CompleteAllTrackedJobs(); Store.CompleteAccess(); }
            public void Dispose() { World.Dispose(); Store.Dispose(); definitions.Dispose(); relations.Dispose(); }
        }
        static WeaponDef Definition() => new WeaponDef { Index = 1, Delivery = WeaponDelivery.Projectile,
            TriggerIntervalTicks = 24, BurstCount = 1, PelletsPerRound = 1, ProjectileSpeed = 100,
            LifetimeMinTicks = 6000, LifetimeMaxTicks = 6000, BeamPulseTicks = 9, BeamWidth = .2f };

        [Test] public void Rate_ExactOverAMinute()
        {
            using var f = new Fixture(Definition()); f.Step(3600);
            Assert.AreEqual(150, f.State.ShotCounter); Assert.AreEqual(150, f.Store.Rows.Length);
            Assert.AreEqual(24, f.State.CooldownTicks);
        }
        [Test] public void Burst_Spacing()
        {
            var weapon = Definition(); weapon.BurstCount = 3; weapon.BurstIntervalTicks = 3;
            using var f = new Fixture(weapon); var released = new List<int>();
            for (int tick = 0; tick < 10; tick++) { f.Step(); if (f.State.Firing != 0) released.Add(tick); }
            CollectionAssert.AreEqual(new[] { 0,3,6 }, released);
        }
        [Test] public void Unpowered_NoFire()
        {
            using var f = new Fixture(Definition()); f.Em.AddComponent<Powered>(f.Mount); f.Em.SetComponentEnabled<Powered>(f.Mount, false);
            f.Step(60); Assert.AreEqual(0, f.State.ShotCounter);
            f.Em.SetComponentEnabled<Powered>(f.Mount, true); f.Em.AddComponent<Hulk>(f.Root);
            f.Step(60); Assert.AreEqual(0, f.State.ShotCounter); Assert.AreEqual(0, f.Store.Rows.Length);
            f.Em.RemoveComponent<Hulk>(f.Root); f.Step(); Assert.AreEqual(1, f.State.ShotCounter);
        }
        [Test] public void Beam_PulseLength()
        {
            var weapon = Definition(); weapon.Delivery = WeaponDelivery.Beam; weapon.TriggerIntervalTicks = 1;
            using var f = new Fixture(weapon); f.Step();
            var pulse = f.Em.GetComponentData<BeamState>(f.Mount);
            Assert.AreEqual(1, pulse.Active); Assert.AreEqual(8, pulse.TicksLeft, "The release tick consumes the first pulse step."); Assert.AreEqual(.2f, pulse.Width);
            Assert.AreEqual(0, f.Store.Rows.Length, "Projectile handler must ignore beam deliveries.");
            for (int tick = 1; tick < 9; tick++)
            {
                f.Step(); pulse = f.Em.GetComponentData<BeamState>(f.Mount);
                Assert.AreEqual(1, f.State.ShotCounter, "Active pulse cannot retrigger.");
                Assert.AreEqual(8 - tick, pulse.TicksLeft);
                Assert.AreEqual(tick < 8 ? 1 : 0, pulse.Active);
            }
            f.Step(); Assert.AreEqual(2, f.State.ShotCounter); Assert.AreEqual(8, f.Em.GetComponentData<BeamState>(f.Mount).TicksLeft);
        }
        [Test] public void ShotIds_Unique()
        {
            var weapon = Definition(); weapon.PelletsPerRound = 4; weapon.BurstCount = 2;
            using var f = new Fixture(weapon); var secondMount = f.AddMount(1);
            f.Em.AddComponent<SecondChunk>(secondMount); f.Step();
            Assert.AreEqual(16, f.Store.Rows.Length); var ids = new HashSet<uint>();
            foreach (var row in f.Store.Rows) { Assert.IsTrue(ids.Add(row.ShotId)); Assert.AreEqual(f.Root, row.Source); Assert.AreEqual(f.Root, row.Instigator); }
            for (int i = 0; i < 8; i++) Assert.AreEqual((uint)i + 1, f.Store.Rows[i].ShotId);
            for (int i = 8; i < 16; i++) Assert.AreEqual(0x10000u + (uint)i - 7, f.Store.Rows[i].ShotId);
        }
        [Test] public void StoreFull_Drops()
        {
            var weapon = Definition(); weapon.PelletsPerRound = 4;
            using var f = new Fixture(weapon, 2); f.Step();
            Assert.AreEqual(2, f.Store.Rows.Length); Assert.AreEqual(2, f.Store.Stats.Value.DroppedFull);
            Assert.AreEqual(2, f.Store.Stats.Value.Fired); Assert.AreEqual(1u, f.Store.Rows[0].ShotId); Assert.AreEqual(2u, f.Store.Rows[1].ShotId);
        }
        [Test] public void Deterministic_FramePatterns()
        {
            ulong[] expected = null;
            foreach (int ticksPerFrame in new[] { 1,2,6 })
            {
                var weapon = Definition(); weapon.SpreadRad = .2f; weapon.LifetimeMinTicks = 600; weapon.LifetimeMaxTicks = 900;
                using var f = new Fixture(weapon);
                for (int tick = 0; tick < 120; tick += ticksPerFrame) f.World.StepFrame(ticksPerFrame * EcsTestWorld.TickSeconds);
                f.Em.CompleteAllTrackedJobs();
                using var query = f.Em.CreateEntityQuery(typeof(FireCapture)); var capture = query.GetSingleton<FireCapture>();
                var hashes = capture.Hashes.AsArray().ToArray(); Assert.AreEqual(120, hashes.Length);
                if (expected == null) expected = hashes; else CollectionAssert.AreEqual(expected, hashes, "Every tick must match, independent of frame grouping.");
            }
        }
        [Test] public void CustomDelivery_ReceivesOnlyItsShots()
        {
            var weapon = Definition(); weapon.Delivery = (WeaponDelivery)250;
            using var f = new Fixture(weapon); f.AddMount(1, 2); var beamMount = f.AddMount(2, 3); f.Step();
            using var query = f.Em.CreateEntityQuery(typeof(FireCapture));
            Assert.AreEqual(1, query.GetSingleton<FireCapture>().CustomCount.Value, "Custom handler receives only its own delivery among all three kinds.");
            Assert.AreEqual(1, f.Store.Rows.Length); Assert.AreEqual(2, f.Store.Rows[0].Weapon);
            Assert.AreEqual(0, f.Em.GetComponentData<BeamState>(f.Mount).Active);
            Assert.AreEqual(1, f.Em.GetComponentData<BeamState>(beamMount).Active);
        }
    }

    struct SecondChunk : IComponentData { }
    struct FireCapture : IComponentData { public NativeList<ulong> Hashes; public NativeReference<int> CustomCount; }
    [DisableAutoCreation, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(ProjectileShotHandlerSystem)), UpdateAfter(typeof(BeamShotHandlerSystem))]
    public partial struct FireCaptureSystem : ISystem
    {
        FireCapture owned;
        public void OnCreate(ref SystemState state)
        {
            owned = new FireCapture { Hashes = new NativeList<ulong>(4096, Allocator.Persistent), CustomCount = new NativeReference<int>(Allocator.Persistent) };
            state.EntityManager.CreateSingleton(owned); state.RequireForUpdate<ProjectileStore>(); state.RequireForUpdate<WeaponShots>();
        }
        public void OnUpdate(ref SystemState state)
        {
            var store = SystemAPI.GetSingleton<ProjectileStore>(); var shots = SystemAPI.GetSingleton<WeaponShots>();
            state.Dependency = new CaptureJob { Rows = store.Rows, Shots = shots.Shots, Capture = owned }.Schedule(store.BeginRead(shots.Access.BeginRead(state.Dependency)));
            store.EndRead(state.Dependency); shots.Access.EndRead(state.Dependency);
        }
        public void OnDestroy(ref SystemState state) { state.CompleteDependency(); owned.Hashes.Dispose(); owned.CustomCount.Dispose(); }
        struct CaptureJob : IJob
        {
            [ReadOnly] public NativeList<ProjectileRow> Rows;
            [ReadOnly] public NativeList<WeaponShot> Shots;
            public FireCapture Capture;
            public void Execute()
            {
                ulong hash = (ulong)Rows.Length;
                foreach (var row in Rows)
                {
                    hash = StableHash.Combine(hash, row.ShotId); hash = StableHash.Combine(hash, row.DeathTick);
                    hash = StableHash.Combine(hash, math.asuint(row.Position.x)); hash = StableHash.Combine(hash, math.asuint(row.Position.y)); hash = StableHash.Combine(hash, math.asuint(row.Position.z));
                    hash = StableHash.Combine(hash, math.asuint(row.Velocity.x)); hash = StableHash.Combine(hash, math.asuint(row.Velocity.y)); hash = StableHash.Combine(hash, math.asuint(row.Velocity.z));
                    hash = StableHash.Combine(hash, math.asuint(row.DamageMultiplier)); hash = StableHash.Combine(hash, (ulong)row.Flags);
                }
                Capture.Hashes.Add(hash);
                foreach (var shot in Shots) if (shot.Delivery == (WeaponDelivery)250) Capture.CustomCount.Value++;
            }
        }
    }
}
