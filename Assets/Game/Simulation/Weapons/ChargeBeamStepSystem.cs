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
    /// <summary>SPEC-21 §4.6d (WPN-019): charge beams. Every tick of a sequence the beam turns toward the weapon's aim at its track
    /// rate and casts once; while charging it only marks what it is on, while firing it damages the first hit as a beam does.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(BeamStepSystem)), UpdateBefore(typeof(ExplosionSystem))]
    public partial struct ChargeBeamStepSystem : ISystem
    {
        EntityQuery query;
        ChargeBeamStepJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            query = new EntityQueryBuilder(Allocator.Temp).WithAll<FunctionalBlock, WeaponMount, WeaponAim>().WithAllRW<ChargeBeamState>().Build(ref state);
            job.Entities = state.GetEntityTypeHandle(); job.Functionals = state.GetComponentTypeHandle<FunctionalBlock>(true);
            job.Mounts = state.GetComponentTypeHandle<WeaponMount>(true); job.Aims = state.GetComponentTypeHandle<WeaponAim>(true);
            job.Charges = state.GetComponentTypeHandle<ChargeBeamState>(); job.HitTest = CombatHitTest.Create(ref state);
            job.Hulks = state.GetComponentLookup<Hulk>(true); job.Owners = state.GetComponentLookup<OwnedBy>(true);
            job.Multipliers = state.GetComponentLookup<ShipStatMultipliers>(true); job.Players = state.GetComponentLookup<PlayerShip>(true);
            job.Velocities = state.GetComponentLookup<PhysicsVelocity>(true);
            job.Power = state.GetComponentLookup<Powered>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>();
            state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<ShieldHitStreams>(); state.RequireForUpdate<SpatialHash>();
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate<FxEventQueue>(); state.RequireForUpdate<SfxEventQueue>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            if (query.IsEmptyIgnoreFilter) return;
            job.Entities.Update(ref state); job.Functionals.Update(ref state); job.Mounts.Update(ref state);
            job.Aims.Update(ref state); job.Charges.Update(ref state); job.HitTest.Update(ref state);
            job.Hulks.Update(ref state); job.Power.Update(ref state); job.Owners.Update(ref state);
            job.Multipliers.Update(ref state); job.Players.Update(ref state); job.Velocities.Update(ref state);
            job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value;
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var shields = SystemAPI.GetSingleton<ShieldHitStreams>();
            var hash = SystemAPI.GetSingleton<SpatialHash>(); var fx = SystemAPI.GetSingleton<FxEventQueue>();
            int chunks = query.CalculateChunkCountWithoutFiltering();
            job.Damage = streams.Damage.BeginWrite(ref state, Game.Data.Combat.ProducerKind.Beam, chunks);
            job.Shields = shields.Hits.BeginWrite(ref state, Game.Data.Combat.ProducerKind.Beam, chunks);
            var sfx = SystemAPI.GetSingleton<SfxEventQueue>();
            job.Hash = hash.AsReadOnly(); job.Fx = fx.Queue.AsParallelWriter(); job.Sfx = sfx.Queue.AsParallelWriter();
            job.MaxCandidates = SystemAPI.TryGetSingleton<WeaponTuning>(out var tuning) ? tuning.MaxCandidates : 32;
            job.Dt = Game.Core.SimTime.FixedTimestep;
            state.Dependency = job.ScheduleParallel(query, sfx.Access.BeginRead(fx.Access.BeginRead(hash.BeginRead(state.Dependency))));
            hash.EndRead(state.Dependency); fx.Access.EndRead(state.Dependency); sfx.Access.EndRead(state.Dependency);
            streams.Damage.EndWrite(state.Dependency); shields.Hits.EndWrite(state.Dependency);
        }
    }
}
