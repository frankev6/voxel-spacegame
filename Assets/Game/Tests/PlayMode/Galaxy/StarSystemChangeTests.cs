using Game.Core;
using Game.Core.Events;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Systems;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.PlayMode.Galaxy
{
    public sealed class StarSystemChangeTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() => EventHub.Reset();
        static StarSystemId Destination(StarSystemLoadTests.Fixture fixture)
        {
            foreach (var system in fixture.Galaxy.Galaxy.StarSystems)
                if (!system.Id.Equals(fixture.State.CurrentStarSystem)) return system.Id;
            Assert.Fail("Fixture needs two real systems"); return default;
        }
        static Entity Player(StarSystemLoadTests.Fixture fixture)
        {
            var entity = fixture.Em.CreateEntity(typeof(PlayerShip), typeof(LocalTransform), typeof(PhysicsVelocity), typeof(UniversePosition));
            fixture.Em.SetComponentData(entity, LocalTransform.FromPositionRotation(new float3(10, 0, 20), Planar.YawRotation(.3f)));
            fixture.Em.SetComponentData(entity, new PhysicsVelocity { Linear = new float3(1, 0, 3) });
            return entity;
        }
        static void Finish(StarSystemLoadTests.Fixture fixture)
        {
            fixture.World.GetExistingSystem<OriginRebaseSystem>().Update(fixture.World.Unmanaged);
            fixture.Em.CompleteAllTrackedJobs(); fixture.Tick();
            Assert.IsTrue(fixture.Loader.IsReady);
        }
        [Test] public void Change_PlacesPlayer()
        {
            using var f = new StarSystemLoadTests.Fixture(); f.Load(); var player = Player(f);
            var destination = Destination(f); var arrival = new double3(123456789.125, 0, -99999.875);
            Assert.IsTrue(f.Loader.BeginStarSystemChange(destination, arrival, 1.2f, StarSystemEntryReason.Jump));
            Assert.IsFalse(f.Loader.BeginStarSystemChange(destination, arrival, 1.2f, StarSystemEntryReason.Jump));
            using var info = f.Em.CreateEntityQuery(typeof(CurrentStarSystemInfo)); Assert.AreEqual(1, info.GetSingleton<CurrentStarSystemInfo>().Changing);
            Finish(f);
            Assert.IsTrue(f.Em.Exists(player)); Assert.AreEqual(arrival, f.Em.GetComponentData<UniversePosition>(player).Value);
            var pose = f.Em.GetComponentData<LocalTransform>(player);
            Assert.Less(math.length(pose.Position), .001f);
            Assert.AreEqual(1.2f, Planar.YawFromForward(math.mul(pose.Rotation, new float3(0, 0, 1))), .0001f);
            Assert.AreEqual(destination, f.State.CurrentStarSystem); Assert.AreEqual(arrival, f.State.PlayerLocation.PositionM);
            Assert.AreEqual(0, info.GetSingleton<CurrentStarSystemInfo>().Changing);
        }
        [Test] public void Change_UnloadsOld()
        {
            using var f = new StarSystemLoadTests.Fixture(); f.Load(); var player = Player(f); var old = f.State.CurrentStarSystem;
            var hulk = f.Em.CreateEntity(typeof(Hulk), typeof(SplitOrigin));
            f.Em.SetComponentData(hulk, new SplitOrigin { Parent = player });
            var disabled = f.Em.CreateEntity(typeof(Disabled)); f.Em.AddSharedComponent(disabled, new StarSystemMember { Id = old });
            using var query = f.Em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<StarSystemMember>() }, Options = EntityQueryOptions.IncludeDisabledEntities });
            using var previous = query.ToEntityArray(Allocator.Temp);
            Assert.IsTrue(f.Loader.BeginStarSystemChange(Destination(f), double3.zero, 0, StarSystemEntryReason.Jump)); Finish(f);
            foreach (var entity in previous) Assert.IsFalse(f.Em.Exists(entity), "An old-system entity survived unload");
            Assert.IsFalse(f.Em.Exists(hulk), "Player debris must stay in the departing system");
            Assert.IsFalse(f.Em.Exists(disabled)); Assert.IsTrue(f.Em.Exists(player));
            query.SetSharedComponentFilter(new StarSystemMember { Id = old }); Assert.AreEqual(0, query.CalculateEntityCount());
        }
        [Test] public void Change_EnteredOnce()
        {
            using var f = new StarSystemLoadTests.Fixture(); f.Load(); EventBus<StarSystemEntered>.Drain(); Player(f);
            int entered = 0; var destination = Destination(f);
            EventBus<StarSystemEntered>.Subscribe(value => { entered++; Assert.AreEqual(destination.Value, value.StarSystemValue); Assert.AreEqual(StarSystemEntryReason.Jump, value.Reason); });
            Assert.IsTrue(f.Loader.BeginStarSystemChange(destination, double3.zero, 0, StarSystemEntryReason.Jump));
            f.Tick(); EventBus<StarSystemEntered>.Drain(); Assert.AreEqual(0, entered, "Must wait for origin placement");
            Finish(f); f.Tick(); f.Tick(); EventBus<StarSystemEntered>.Drain(); Assert.AreEqual(1, entered);
        }
    }
}
