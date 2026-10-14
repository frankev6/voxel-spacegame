using System;
using System.Collections.Generic;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Data;
using Game.Galaxy.Interaction;
using Game.Galaxy.Services;
using Game.Simulation.Commands;
using Game.Simulation.Spatial;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.PlayMode.Galaxy
{
    /// <summary>SYS-008: real distance queries and direct variable-rate updates, no wall-clock waits.</summary>
    public sealed class InteractionTests
    {
        sealed class Fixture : IDisposable
        {
            internal readonly World World = new("SYS-008 interaction");
            internal EntityManager Em => World.EntityManager;
            internal readonly Entity Player, Station, Command;
            internal readonly NativeQueue<ManagedEventEnvelope<BodyInteracted>> Events = new(Allocator.Persistent);
            readonly SpatialHash spatial;
            readonly PhysicsWorld physics;
            readonly BlobAssetReference<Collider> collider;
            readonly SystemHandle system;
            readonly float radius;
            internal Fixture(float stationRadius = 150, BodyId body = default)
            {
                radius = stationRadius;
                Em.CreateEntity(typeof(GameplayActive), typeof(WorldSceneActive));
                Command = Em.CreateSingleton<PlayerCommand>();
                Em.CreateSingleton(new StreamingConfig { InteractionQueryRadiusM = 1000 });
                Em.CreateSingleton(new ManagedEventQueue<BodyInteracted> { Queue = Events });
                Player = Em.CreateEntity(typeof(PlayerShip), typeof(LocalTransform));
                Station = Em.CreateEntity(typeof(Interactable), typeof(PersistentId));
                Em.SetComponentData(Station, new PersistentId(77));
                Em.SetComponentData(Station, new Interactable { Body = body, Kind = InteractionKind.Dock, DistanceM = 150 });
                spatial = SpatialHash.Create(100);
                spatial.Proxies.Add(new SpatialProxy { Entity = Player, HullRadius = 12, Radius = 12 });
                Em.CreateSingleton(spatial);
                collider = BoxCollider.Create(new BoxGeometry { Size = new float3(2 * radius), Orientation = quaternion.identity }, PhysicsLayers.Grid);
                physics = new PhysicsWorld(1, 0, 0);
                var bodies = physics.StaticBodies;
                bodies[0] = new RigidBody { Entity = Station, Collider = collider, WorldFromBody = RigidTransform.identity, Scale = 1 };
                var build = physics; build.CollisionWorld.BuildBroadphase(ref build, 1f / 60, float3.zero);
                Em.CreateSingleton(new PhysicsWorldSingleton { PhysicsWorld = physics });
                system = World.CreateSystem<InteractionQuerySystem>(); Gap(100);
            }
            internal void Gap(float gap)
            {
                Em.CompleteAllTrackedJobs(); spatial.CompleteAccess();
                float3 center = new float3(0, 0, -(radius + 12 + gap));
                // Offset the root to prove that the spatial hull centre, not root position, is used.
                Em.SetComponentData(Player, LocalTransform.FromPosition(center + new float3(0, 0, -30)));
                var proxies = spatial.Proxies;
                proxies[0] = new SpatialProxy { Entity = Player, Center = center, HullRadius = 12, Radius = 12 };
            }
            internal InteractionTarget Frame(byte counter)
            {
                Em.SetComponentData(Command, new PlayerCommand { Edges = new PlayerEdgeCounters { Interact = counter } });
                system.Update(World.Unmanaged); Em.CompleteAllTrackedJobs();
                using var target = Em.CreateEntityQuery(typeof(InteractionTarget)); return target.GetSingleton<InteractionTarget>();
            }
            public void Dispose()
            { Em.CompleteAllTrackedJobs(); spatial.CompleteAccess(); World.Dispose(); physics.Dispose(); collider.Dispose(); spatial.Dispose(); Events.Dispose(); }
        }
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() => EventHub.Reset();
        [Test] public void R10_SurfaceDistance()
        {
            foreach (float radius in new[] { 150f, 20f })
            {
                using var f = new Fixture(radius);
                var target = f.Frame(0); Assert.AreEqual(1, target.InRange); Assert.AreEqual(100, target.DistanceM, .01f);
                Assert.AreEqual(77, target.GridPersistentId);
                f.Gap(200); target = f.Frame(0); Assert.AreEqual(0, target.InRange); Assert.AreEqual(f.Station, target.Target);
                Assert.AreEqual(200, target.DistanceM, .01f);
            }
        }
        [Test] public void OnePressOneEvent_FrameRates()
        {
            foreach (int fps in new[] { 30, 60, 144 })
            {
                using var f = new Fixture();
                using var input = f.Em.CreateEntityQuery(typeof(InteractionInputState));
                input.SetSingleton(new InteractionInputState { LastInteractCounter = 253, Initialized = 1 });
                for (int frame = 0; frame < fps; frame++)
                    f.Frame(frame < fps / 3 ? (byte)254 : frame < 2 * fps / 3 ? (byte)255 : (byte)0);
                Assert.AreEqual(3, f.Events.Count, "One event per edge, including byte wrap, at " + fps + " FPS.");
            }
        }
        [Test] public void OutOfRange_Dropped()
        {
            using var f = new Fixture(); f.Gap(200); f.Frame(1); Assert.AreEqual(0, f.Events.Count);
            f.Gap(100); f.Frame(1); Assert.AreEqual(0, f.Events.Count);
            f.Em.RemoveComponent<PlayerShip>(f.Player); Assert.AreEqual(Entity.Null, f.Frame(2).Target);
            f.Em.AddComponent<PlayerShip>(f.Player); f.Frame(2); Assert.AreEqual(0, f.Events.Count);
            f.Frame(3); Assert.AreEqual(1, f.Events.Count);
        }
        [Test] public void Dock_SetsState()
        {
            var docking = new DockingService(); var changes = new List<DockStateChanged>();
            EventBus<DockStateChanged>.Subscribe(changes.Add);
            using var loader = new StarSystemLoadTests.Fixture(true, docking: docking); loader.Load(); loader.Stream(0);
            Entity original = loader.Grid();
            using var f = new Fixture(body: loader.Station.Id); f.Frame(1); f.Frame(1);
            Assert.AreEqual(1, f.Events.Count);
            while (f.Events.TryDequeue(out var value)) EventBus<BodyInteracted>.Publish(value.Payload);
            EventBus<BodyInteracted>.Drain(); EventBus<DockStateChanged>.Drain();
            Assert.IsTrue(docking.IsDocked); Assert.AreEqual(loader.Station.Id, docking.DockedBody); Assert.AreEqual(1, changes.Count);
            docking.Dock(loader.Station.Id); EventBus<DockStateChanged>.Drain(); Assert.AreEqual(1, changes.Count);
            CollectionAssert.Contains(loader.State.GetOrCreateDelta(loader.Station.Id.System).Interacted, loader.Station.Id.LocalKey);
            loader.Tick(); Assert.AreEqual(original, loader.Grid(), "Persisting interaction must not rebuild the station.");
            loader.Unload(); EventBus<DockStateChanged>.Drain(); Assert.IsFalse(docking.IsDocked); Assert.AreEqual(2, changes.Count);
        }
    }
}
