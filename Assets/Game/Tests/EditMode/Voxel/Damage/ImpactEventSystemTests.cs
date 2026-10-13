using System;
using System.Collections.Generic;
using Game.App.Systems;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Streams;
using Game.Tests.Common;
using Game.Voxel.Damage;
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

namespace Game.Tests.EditMode.Voxel.Damage
{
    [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(ImpactEventSystem))]
    public partial class ImpactCaptureSystem : SystemBase
    {
        public NativeList<DamageEvent> Events;
        protected override void OnCreate() { Events = new NativeList<DamageEvent>(Allocator.Persistent); RequireForUpdate<DamageStreams>(); }
        protected override void OnUpdate()
        {
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var set = streams.Damage.BeginRead(ref CheckedStateRef);
            Dependency = SimEventGather.Schedule(set, Events, Dependency); streams.Damage.EndRead(Dependency);
        }
        public void Finish() => Dependency.Complete();
        protected override void OnDestroy() { Dependency.Complete(); Events.Dispose(); }
    }
    public unsafe sealed class ImpactEventSystemTests
    {
        internal sealed class Fixture : IDisposable
        {
            public readonly EcsTestWorld Test;
            public readonly TestGridFactory Grids;
            public readonly ImpactCaptureSystem Capture;
            readonly List<BlobAssetReference<Unity.Physics.Collider>> colliders = new List<BlobAssetReference<Unity.Physics.Collider>>();
            readonly BlobAssetReference<DamageTableBlob> table;
            public EntityManager EM => Test.EntityManager;
            public Fixture()
            {
                var systems = new List<Type> { typeof(GridSpawnSystem), typeof(SimClockAdvanceSystem), typeof(DamageStreamsSystem), typeof(ImpactEventSystem),
                    typeof(ImpactCaptureSystem), typeof(FxEventQueueCreateSystem), typeof(SfxEventQueueCreateSystem) };
                foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                    if (type.Namespace != null && (type.Namespace.StartsWith("Unity.Physics") || type.Namespace == "Unity.Transforms")) systems.Add(type);
                Test = EcsTestWorld.Create(systems.ToArray()); Test.AddGameplayActive(); Test.AddSimClock();
                Grids = new TestGridFactory(Test.World, includeArmored: true); Capture = Test.World.GetExistingSystemManaged<ImpactCaptureSystem>();
                var step = PhysicsStep.Default; step.Gravity = 0; Test.AddSingleton(step);
                using var builder = new BlobBuilder(Allocator.Temp); ref var blob = ref builder.ConstructRoot<DamageTableBlob>(); blob.ArmorClassCount = 2;
                var types = builder.Allocate(ref blob.Types, 4); var multipliers = builder.Allocate(ref blob.ArmorMultipliers, 8);
                for (int i = 0; i < 4; i++) { types[i] = new DamageTypeRow { ArmorOffset = i * 2 }; multipliers[i * 2] = 1; multipliers[i * 2 + 1] = .5f; }
                table = builder.CreateBlobAssetReference<DamageTableBlob>(Allocator.Persistent);
                using var configQuery = EM.CreateEntityQuery(typeof(DamageConfig)); var config = configQuery.GetSingleton<DamageConfig>();
                config.Table = table; config.CueRamImpact = new ContentHash(91); configQuery.SetSingleton(config);
            }
            public Entity Body(float3 position, float3 velocity, bool armored = false, float mass = 20000)
            {
                var source = Grids.Box(0, 0);
                if (armored) { source.SetCell(0, default); Grids.Place(source, "block_test_armored", 0); }
                var root = Grids.Spawn(source);
                var collider = Unity.Physics.BoxCollider.Create(new BoxGeometry { Center = new float3(.25f), Size = new float3(.5f), Orientation = quaternion.identity, BevelRadius = 0 },
                    PhysicsLayers.Grid, PhysicsLayers.GridMaterial); colliders.Add(collider);
                EM.AddComponentData(root, new PhysicsCollider { Value = collider }); EM.AddComponentData(root, PhysicsMass.CreateDynamic(collider.Value.MassProperties, mass));
                EM.AddComponentData(root, new PhysicsVelocity { Linear = velocity }); EM.AddSharedComponent(root, new PhysicsWorldIndex(0));
                EM.SetComponentData(root, LocalTransform.FromPosition(position)); return root;
            }
            public void Tick(int count = 1) { Test.StepTicks(count); Capture.Finish(); }
            public void Frame(float dt) { Test.StepFrame(dt); Capture.Finish(); }
            public void HitAgain(Entity a, Entity b)
            {
                EM.SetComponentData(a, LocalTransform.FromPosition(new float3(-.5f,0,0))); EM.SetComponentData(b, LocalTransform.Identity);
                EM.SetComponentData(a, new PhysicsVelocity { Linear = new float3(20,0,0) }); EM.SetComponentData(b, new PhysicsVelocity { Linear = new float3(-20,0,0) });
            }
            public void Dispose() { Test.Dispose(); foreach (var collider in colliders) { var c = collider; c.Dispose(); } table.Dispose(); Grids.Dispose(); }
        }
        [Test] public void HeadOn_OneEventPerSide()
        {
            using var f = new Fixture(); var a = f.Body(new float3(-.5f,0,0), new float3(20,0,0)); var b = f.Body(0, new float3(-20,0,0));
            f.EM.AddComponentData(b, new Game.Data.Components.OwnedBy { Owner = a });
            for (int tick = 0; tick < 3 && f.Capture.Events.Length == 0; tick++) f.Tick();
            Assert.AreEqual(2, f.Capture.Events.Length); var x = f.Capture.Events[0]; var y = f.Capture.Events[1];
            Assert.AreEqual(a, x.Grid); Assert.AreEqual(b, y.Grid); Assert.AreEqual(a, x.Instigator); Assert.AreEqual(DamageType.Ram, x.Type);
            Assert.AreEqual(DamageEventFlags.NoImpulse, x.Flags); Assert.AreEqual(float3.zero, x.WorldImpulse);
            Assert.Less(x.LocalDir.x, 0); Assert.Greater(y.LocalDir.x, 0);
            Assert.That(x.LocalPoint.x, Is.InRange(0f,.5f)); Assert.That(y.LocalPoint.x, Is.InRange(0f,.5f));
            int initial = f.Capture.Events.Length;
            for (int tick = 0; tick < 5; tick++) { f.HitAgain(a,b); f.Tick(); Assert.AreEqual(initial, f.Capture.Events.Length, "Pair cooldown"); }
            f.HitAgain(a,b); f.Tick(); Assert.AreEqual(4, f.Capture.Events.Length);
            var fx = f.Test.GetSingleton<FxEventQueue>(); fx.Access.CompleteAccess(); Assert.AreEqual(2, fx.Queue.Count);
            Assert.AreEqual(Game.Data.Definitions.FxKind.RamImpact, fx.Queue.Dequeue().Kind);
            Assert.AreEqual(2, f.Test.GetSingleton<SfxEventQueue>().Queue.Count);
            TestContext.WriteLine($"20t head-on: damage={x.Amount}/{y.Amount}, radius={x.Radius}; cooldown=6 ticks");
        }
        [Test] public void RestingContact_NoDamage()
        {
            using var f = new Fixture(); var a = f.Body(new float3(-.5f,0,0), 0); var b = f.Body(0, 0);
            for (int tick = 0; tick < 30; tick++)
            {
                // Repeated small thrust steps pressing two bodies together, without resetting their poses.
                f.EM.SetComponentData(a, new PhysicsVelocity { Linear = new float3(.01f,0,0) });
                f.EM.SetComponentData(b, new PhysicsVelocity { Linear = new float3(-.01f,0,0) }); f.Tick();
            }
            Assert.AreEqual(0, f.Capture.Events.Length); Assert.AreEqual(0, f.Test.GetSingleton<FxEventQueue>().Queue.Count);
        }
        [Test] public void HardnessSplit_FavoursArmour()
        {
            using var f = new Fixture(); var hull = f.Body(new float3(-.5f,0,0), new float3(20,0,0)); var armor = f.Body(0, new float3(-20,0,0), armored: true);
            for (int tick = 0; tick < 3 && f.Capture.Events.Length == 0; tick++) f.Tick();
            Assert.AreEqual(2, f.Capture.Events.Length); var a = f.Capture.Events[0]; var b = f.Capture.Events[1];
            Assert.AreEqual(hull, a.Grid); Assert.AreEqual(armor, b.Grid); Assert.Greater(a.Amount, b.Amount); Assert.That(a.Amount / b.Amount, Is.EqualTo(2).Within(.001f));
        }
        [Test] public void PendingImpulse_AppliedNextTick()
        {
            using var f = new Fixture(); var body = f.Body(0, 0, mass: 1000);
            f.EM.SetComponentData(body, new PendingImpulse { Tick = 0, LinearWorld = new float3(1000,0,0) });
            f.Tick(); Assert.AreEqual(float3.zero, f.EM.GetComponentData<PhysicsVelocity>(body).Linear);
            f.Tick(); Assert.That(f.EM.GetComponentData<PhysicsVelocity>(body).Linear.x, Is.EqualTo(1).Within(1e-6f));
            f.Tick(); Assert.That(f.EM.GetComponentData<PhysicsVelocity>(body).Linear.x, Is.EqualTo(1).Within(1e-6f));
            Assert.AreEqual(new float3(1000,0,0), f.EM.GetComponentData<PendingImpulse>(body).LinearWorld, "Only resolver writes pending impulses");
        }
        [Test] public void Planar_ImpulseProjected()
        {
            using var f = new Fixture(); var body = f.Body(0, 0, mass: 1000); f.EM.AddComponent<Game.Data.Components.PlanarConstraint>(body);
            f.EM.SetComponentData(body, new PendingImpulse { Tick = 0, LinearWorld = new float3(1000,2000,3000), AngularWorld = new float3(10,20,30) });
            f.Tick(2); var velocity = f.EM.GetComponentData<PhysicsVelocity>(body);
            Assert.That(math.distance(new float3(1,0,3), velocity.Linear), Is.LessThan(1e-6f)); Assert.AreEqual(0, velocity.Angular.x); Assert.AreEqual(0, velocity.Angular.z); Assert.Greater(velocity.Angular.y, 0);
        }
        [Test] public void RamDamage_FrameRateIndependent()
        {
            double? total = null; int? count = null;
            foreach (int fps in new[] {30,60,144})
            {
                using var f = new Fixture(); f.Body(new float3(-1.5f,0,0), new float3(20,0,0)); f.Body(0, new float3(-20,0,0));
                for (int frame = 0; frame < fps * 2; frame++) f.Frame(1f / fps);
                Assert.AreEqual(120, f.Test.TicksRun); double damage = 0; foreach (var e in f.Capture.Events) damage += e.Amount;
                Assert.Greater(damage, 0);
                if (total.HasValue) { Assert.AreEqual(total.Value, damage); Assert.AreEqual(count.Value, f.Capture.Events.Length); }
                total = damage; count = f.Capture.Events.Length; TestContext.WriteLine($"{fps} FPS: {count} ram events, {damage} damage");
            }
        }
    }
}
