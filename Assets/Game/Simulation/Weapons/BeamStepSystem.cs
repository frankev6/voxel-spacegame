using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.7: parallel first-hit beam pulses, guarded hull/shield streams and clipped endpoints.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(ProjectileStepSystem)), UpdateBefore(typeof(DamageResolveSystem))]
    public partial struct BeamStepSystem : ISystem
    {
        EntityQuery query;
        BeamStepJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            query = new EntityQueryBuilder(Allocator.Temp).WithAll<FunctionalBlock, WeaponMount, WeaponAim>().WithAllRW<BeamState>().Build(ref state);
            job.Entities = state.GetEntityTypeHandle(); job.Functionals = state.GetComponentTypeHandle<FunctionalBlock>(true);
            job.Mounts = state.GetComponentTypeHandle<WeaponMount>(true); job.Aims = state.GetComponentTypeHandle<WeaponAim>(true);
            job.Beams = state.GetComponentTypeHandle<BeamState>(); job.HitTest = CombatHitTest.Create(ref state);
            job.Hulks = state.GetComponentLookup<Hulk>(true); job.Owners = state.GetComponentLookup<OwnedBy>(true);
            job.Multipliers = state.GetComponentLookup<ShipStatMultipliers>(true); job.Players = state.GetComponentLookup<PlayerShip>(true);
            job.Velocities = state.GetComponentLookup<PhysicsVelocity>(true);
            job.Power = state.GetComponentLookup<Powered>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>();
            state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<ShieldHitStreams>(); state.RequireForUpdate<SpatialHash>();
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate<FxEventQueue>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            if (query.IsEmptyIgnoreFilter) return;
            job.Entities.Update(ref state); job.Functionals.Update(ref state); job.Mounts.Update(ref state);
            job.Aims.Update(ref state); job.Beams.Update(ref state); job.HitTest.Update(ref state);
            job.Hulks.Update(ref state); job.Power.Update(ref state); job.Owners.Update(ref state);
            job.Multipliers.Update(ref state); job.Players.Update(ref state); job.Velocities.Update(ref state);
            job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value;
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var shields = SystemAPI.GetSingleton<ShieldHitStreams>();
            var hash = SystemAPI.GetSingleton<SpatialHash>(); var fx = SystemAPI.GetSingleton<FxEventQueue>();
            int chunks = query.CalculateChunkCountWithoutFiltering();
            job.Damage = streams.Damage.BeginWrite(ref state, Game.Data.Combat.ProducerKind.Beam, chunks);
            job.Shields = shields.Hits.BeginWrite(ref state, Game.Data.Combat.ProducerKind.Beam, chunks);
            job.Hash = hash.AsReadOnly(); job.Fx = fx.Queue.AsParallelWriter();
            job.MaxCandidates = SystemAPI.TryGetSingleton<WeaponTuning>(out var tuning) ? tuning.MaxCandidates : 32;
            state.Dependency = job.ScheduleParallel(query, fx.Access.BeginRead(hash.BeginRead(state.Dependency)));
            hash.EndRead(state.Dependency); fx.Access.EndRead(state.Dependency);
            streams.Damage.EndWrite(state.Dependency); shields.Hits.EndWrite(state.Dependency);
        }
    }
}
