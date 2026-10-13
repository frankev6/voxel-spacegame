using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Systems;
using Game.Simulation.Commands;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>Ten required GAL-009 functional scenarios exercise production jobs and public reset commands.</summary>
    public sealed class OriginRebaseSystemTests
    {
        EcsTestWorld world;
        NativeQueue<ManagedEventEnvelope<OriginShifted>> events;
        Entity origin;
        GalaxyService service;
        EntityManager Em => world.EntityManager;
        FrameOrigin Frame => Em.GetComponentData<FrameOrigin>(origin);
        [SetUp] public void SetUp()
        {
            world = EcsTestWorld.Create(typeof(SimClockAdvanceSystem), typeof(PendingRebaseWriteSystem), typeof(OriginRebaseSystem));
            world.AddGameplayActive(); world.AddWorldSceneActive(); world.AddSimClock();
            events = new NativeQueue<ManagedEventEnvelope<OriginShifted>>(Allocator.Persistent);
            world.AddSingleton(new ManagedEventQueue<OriginShifted> { Queue = events });
            using var query = Em.CreateEntityQuery(typeof(FrameOrigin)); origin = query.GetSingletonEntity();
            var report = new ValidationReport("Origin test");
            var catalog = ContentCatalog.Build(new ContentDefinition[] { Block("block_missing", BlockKind.Structural), Item("item_missing"), Create<GalaxyGenerationDefinition>("galaxy_test") }, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            service = new GalaxyService(catalog, new GalaxyState(), world.World, new GalaxyFrameTicker());
        }
        [TearDown] public void TearDown()
        { service?.Dispose(); world?.Dispose(); if (events.IsCreated) events.Dispose(); DestroyAll(); }
        Entity Root(float3 local, bool player = false, bool universal = false)
        {
            var entity = Em.CreateEntity(); Em.AddComponentData(entity, LocalTransform.FromPosition(local));
            if (player) { Em.AddComponent<PlayerShip>(entity); Em.AddComponent<PhysicsVelocity>(entity); }
            if (universal) Em.AddComponentData(entity, new UniversePosition { Value = Frame.OriginInStarSystem + (double3)local });
            return entity;
        }
        void Step() { world.StepFrame(EcsTestWorld.TickSeconds); Em.CompleteAllTrackedJobs(); }
        [Test] public void Threshold_Rebases()
        {
            var player = Root(new float3(2000, 0, 0), true, true);
            Em.AddComponent<PendingRebaseWrite>(player);
            Assert.DoesNotThrow(Step, "Origin gather must depend on the pending transform writer without main-thread synchronization.");
            Assert.AreEqual(float3.zero, Em.GetComponentData<LocalTransform>(player).Position);
            Assert.AreEqual(new double3(2001, 0, 0), Frame.OriginInStarSystem); Assert.AreEqual(1u, Frame.ShiftSequence);
            Assert.AreEqual(1, events.Count); var e = events.Dequeue();
            Assert.AreEqual(new float3(-2001, 0, 0), e.Payload.Delta); Assert.AreEqual(1ul, e.SortKey);
        }
        [Test] public void UniversePosition_ExactAcrossRebases()
        {
            var stationary = Root(new float3(123, 0, -789), universal: true);
            double3 expected = Em.GetComponentData<UniversePosition>(stationary).Value;
            for (int i = 1; i <= 1000; i++)
            { service.RequestOriginReset(StarSystemId.None, new double3(i * 1500, 0, 0)); Step(); Assert.AreEqual(expected, Em.GetComponentData<UniversePosition>(stationary).Value); }
            Assert.AreEqual(1000u, Frame.ShiftSequence);
        }
        [Test] public void Children_Untouched()
        {
            var player = Root(new float3(2001, 0, 0), true); var child = Root(new float3(3, 4, 5), universal: true);
            Em.AddComponentData(child, new Parent { Value = player }); var before = Em.GetComponentData<LocalTransform>(child);
            var up = Em.GetComponentData<UniversePosition>(child); Step();
            Assert.AreEqual(before, Em.GetComponentData<LocalTransform>(child)); Assert.AreEqual(up, Em.GetComponentData<UniversePosition>(child));
        }
        [Test] public void Request_ChangesStarSystem()
        {
            var persistent = Root(new float3(30, 0, 40), universal: true); var transient = Root(new float3(7, 0, 9));
            service.RequestOriginReset(new StarSystemId(2), new double3(10, 0, 20)); Step();
            Assert.AreEqual(new float3(20, 0, 20), Em.GetComponentData<LocalTransform>(persistent).Position);
            Assert.AreEqual(new float3(7, 0, 9), Em.GetComponentData<LocalTransform>(transient).Position);
            Assert.AreEqual(new StarSystemId(2), Frame.StarSystem); Assert.AreEqual(1, Frame.LastShiftChangedStarSystem); Assert.AreEqual(float3.zero, Frame.LastShiftDelta);
        }
        [Test] public void Analytic_NotSynced()
        {
            var body = Root(new float3(3, 0, 4), universal: true); Em.AddComponent<UniversePositionIsAnalytic>(body);
            var exact = new double3(123.123456789, 0, 987.987654321); Em.SetComponentData(body, new UniversePosition { Value = exact });
            Step(); Assert.AreEqual(exact, Em.GetComponentData<UniversePosition>(body).Value);
            service.RequestOriginReset(StarSystemId.None, new double3(100, 0, 100)); Step();
            Assert.AreEqual(exact, Em.GetComponentData<UniversePosition>(body).Value);
            Assert.AreEqual((float3)(exact - Frame.OriginInStarSystem), Em.GetComponentData<LocalTransform>(body).Position);
        }
        [Test] public void Placement_PoseAndVelocity()
        {
            var player = Root(new float3(10, 0, 20), true, true);
            var velocity = new PhysicsVelocity { Linear = new float3(2, 0, 8), Angular = new float3(0, .3f, 0) }; Em.SetComponentData(player, velocity);
            Em.AddComponentData(player, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = RigidTransform.identity });
            var placement = new PlayerPlacement { PositionM = new double3(105, 0, 207), YawRad = math.PI / 2 };
            service.RequestOriginReset(new StarSystemId(2), new double3(100, 0, 200), placement); Step();
            var pose = Em.GetComponentData<LocalTransform>(player); var after = Em.GetComponentData<PhysicsVelocity>(player);
            Assert.AreEqual(new float3(5, 0, 7), pose.Position); Assert.That(math.distance(math.forward(pose.Rotation), new float3(1, 0, 0)), Is.LessThan(.0001f));
            Assert.That(math.distance(after.Linear, math.mul(quaternion.RotateY(placement.YawRad), velocity.Linear)), Is.LessThan(.0001f));
            Assert.AreEqual(velocity.Angular, after.Angular); Assert.AreEqual(placement.PositionM, Em.GetComponentData<UniversePosition>(player).Value);
            Assert.AreEqual(new RigidTransform(pose.Rotation, pose.Position), Em.GetComponentData<PhysicsGraphicalInterpolationBuffer>(player).PreviousTransform);
        }
        [Test] public void SameStarSystemRequest_ShiftsConsumers()
        {
            var transient = Root(new float3(30, 0, 50)); service.RequestOriginReset(StarSystemId.None, new double3(10, 0, 20)); Step();
            var delta = new float3(-10, 0, -20); Assert.AreEqual(delta, Frame.LastShiftDelta); Assert.AreEqual(0, Frame.LastShiftChangedStarSystem);
            Assert.AreEqual(new float3(20, 0, 30), Em.GetComponentData<LocalTransform>(transient).Position);
            Assert.AreEqual(delta, events.Dequeue().Payload.Delta); var record = Em.GetComponentData<OriginShiftRecord>(origin);
            Assert.AreEqual(delta, record.Delta); Assert.AreEqual(Frame.ShiftSequence, record.Sequence); Assert.AreEqual(0, record.StarSystemChanged);
        }
        [Test] public void Record_MirrorsFrameOrigin()
        {
            Root(new float3(2001, 0, 0), true); Step(); CheckRecord();
            service.RequestOriginReset(new StarSystemId(4), new double3(100, 0, 50)); Step(); CheckRecord();
            service.RequestOriginReset(new StarSystemId(4), new double3(150, 0, 70)); Step(); CheckRecord();
        }
        void CheckRecord()
        {
            var r = Em.GetComponentData<OriginShiftRecord>(origin); Assert.AreEqual(Frame.ShiftSequence, r.Sequence);
            Assert.AreEqual(Frame.LastShiftDelta, r.Delta); Assert.AreEqual(Frame.LastShiftChangedStarSystem, r.StarSystemChanged);
        }
        [Test] public void Serials_Returned()
        {
            uint first = service.RequestOriginReset(new StarSystemId(1), new double3(10));
            uint second = service.RequestOriginReset(new StarSystemId(2), new double3(20), new PlayerPlacement());
            Assert.AreEqual(1u, first); Assert.AreEqual(2u, second); Assert.AreEqual(second, service.LastOriginRequestSerial);
            Step(); Assert.AreEqual(second, Frame.AppliedRequestSerial); Assert.AreEqual(new StarSystemId(2), Frame.StarSystem);
            Assert.IsFalse(Em.IsComponentEnabled<OriginRebaseRequest>(origin)); Assert.AreEqual(1, events.Count);
        }
        [Test] public void Smoothing_HistoryShifted()
        {
            var root = Root(new float3(2001, 0, 0), true);
            var history = new RigidTransform(quaternion.RotateY(.5f), new float3(1999, 0, 2));
            Em.AddComponentData(root, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = history }); Step();
            var after = Em.GetComponentData<PhysicsGraphicalInterpolationBuffer>(root).PreviousTransform;
            Assert.AreEqual(history.pos + new float3(-2001, 0, 0), after.pos); Assert.AreEqual(history.rot, after.rot);
        }
    }
    public struct PendingRebaseWrite : IComponentData { }
    [DisableAutoCreation, UpdateInGroup(typeof(GameInitGroup), OrderFirst = true), UpdateBefore(typeof(OriginRebaseSystem))]
    public partial struct PendingRebaseWriteSystem : ISystem
    {
        public void OnUpdate(ref SystemState state) => state.Dependency = new WriteJob().ScheduleParallel(state.Dependency);
        [BurstCompile, WithAll(typeof(PendingRebaseWrite))] partial struct WriteJob : IJobEntity
        { void Execute(ref LocalTransform transform) { transform.Position += new float3(1, 0, 0); } }
    }
}
