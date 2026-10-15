using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Data.Streams;
using Game.Voxel.Damage.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.2 impulses: last tick's damage impulses (PendingImpulse) and this tick's requested impulses become velocity.
    /// ADR-0117 applier, every machine; it was the first half of ImpactEventSystem, whose ram damage stays the authority's. Only bodies
    /// with mass move, so on a co-op guest that is its own predicted ship (cosmetic hits knock it back at once).</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(ImpactEventSystem)), UpdateBefore(typeof(DamageResolveSystem))]
    public unsafe partial struct ImpulseApplySystem : ISystem
    {
        NativeList<ImpulseRequest> requests;
        ApplyRequestedImpulseJob applyRequested;
        ComponentLookup<PlanarConstraint> planar;
        static readonly ProfilerMarker Apply = new ProfilerMarker(ProfilerCategory.Scripts, "ImpulseApplySystem.Apply");
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            requests = new NativeList<ImpulseRequest>(64, Allocator.Persistent);
            applyRequested.Velocities = state.GetComponentLookup<PhysicsVelocity>();
            applyRequested.Masses = state.GetComponentLookup<PhysicsMass>(true);
            applyRequested.Transforms = state.GetComponentLookup<LocalTransform>(true);
            planar = state.GetComponentLookup<PlanarConstraint>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<SimClock>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) { state.CompleteDependency(); requests.Dispose(); }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var data = streams.Data; uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            state.Dependency = data->AllJobs(state.Dependency); planar.Update(ref state);
            using (Apply.Auto()) state.Dependency = new ApplyPendingImpulseJob { Tick = tick, Planar = planar }.ScheduleParallel(state.Dependency);
            applyRequested.Velocities.Update(ref state); applyRequested.Masses.Update(ref state); applyRequested.Transforms.Update(ref state);
            state.Dependency = new ClearRequestsJob { Requests = requests }.Schedule(state.Dependency);
            var requested = streams.Impulses.BeginRead(ref state);
            state.Dependency = SimEventGather.Schedule(requested, requests, state.Dependency);
            applyRequested.Requests = requests; applyRequested.Planar = planar; applyRequested.Data = data;
            using (Apply.Auto()) state.Dependency = applyRequested.Schedule(state.Dependency);
            streams.Impulses.EndRead(state.Dependency);
            data->ImpulseHandle = state.Dependency;
        }
        [BurstCompile] struct ClearRequestsJob : IJob
        { public NativeList<ImpulseRequest> Requests; public void Execute() => Requests.Clear(); }
    }
}
