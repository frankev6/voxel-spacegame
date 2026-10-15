using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Data.Streams;
using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Voxel.Replica
{
    /// <summary>Co-op guests only (SPEC-90, enabled by Game.App's RoleSystemPolicy): weapons still run there for cosmetic shots and
    /// impacts, but damage and restores are the host's. This consumer takes the place of DamageResolveSystem and ImpactEventSystem:
    /// damage and restores are discarded so the channels never go stale, and requested impulses are applied. Only the guest's own
    /// predicted ship has mass there (replicas are kinematic), so hits knock it back at once instead of after a host correction
    /// (SPEC-90 §10a).</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(DamageResolveSystem))]
    public unsafe partial struct ReplicaDamageDrainSystem : ISystem
    {
        NativeList<ImpulseRequest> requests;
        ApplyRequestedImpulseJob apply;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;
            requests = new NativeList<ImpulseRequest>(64, Allocator.Persistent);
            apply.Velocities = state.GetComponentLookup<PhysicsVelocity>();
            apply.Masses = state.GetComponentLookup<PhysicsMass>(true);
            apply.Transforms = state.GetComponentLookup<LocalTransform>(true);
            apply.Planar = state.GetComponentLookup<PlanarConstraint>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) { state.CompleteDependency(); requests.Dispose(); }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            streams.Damage.BeginRead(ref state); streams.Damage.EndRead(state.Dependency);
            streams.Restores.BeginRead(ref state); streams.Restores.EndRead(state.Dependency);
            apply.Velocities.Update(ref state); apply.Masses.Update(ref state); apply.Transforms.Update(ref state); apply.Planar.Update(ref state);
            state.Dependency = new ClearJob { Requests = requests }.Schedule(state.Dependency);
            var requested = streams.Impulses.BeginRead(ref state);
            state.Dependency = SimEventGather.Schedule(requested, requests, state.Dependency);
            apply.Requests = requests; apply.Data = streams.Data;
            state.Dependency = apply.Schedule(state.Dependency);
            streams.Impulses.EndRead(state.Dependency);
        }
        [BurstCompile] struct ClearJob : IJob { public NativeList<ImpulseRequest> Requests; public void Execute() => Requests.Clear(); }
    }
}
