using Game.Core.Components;
using Game.Data.Components;
using Game.Core.Systems;
using Game.Data.Streams;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.5: parallel firing, followed by stable delivery-agnostic stream flattening.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPrePhysicsGroup)), UpdateAfter(typeof(WeaponAimSystem)), UpdateBefore(typeof(AbilityActivationGroup))]
    public partial struct WeaponFireSystem : ISystem, ISystemStartStop
    {
        WeaponShots owned; EntityQuery query; WeaponFireJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            owned = new WeaponShots { Shots = new NativeList<WeaponShot>(128, Allocator.Persistent), Access = GuardedHandles.Create() };
            state.EntityManager.CreateSingleton(owned);
            query = new EntityQueryBuilder(Allocator.Temp).WithAll<FunctionalBlock, WeaponMount, WeaponAim>().WithAllRW<WeaponState>().Build(ref state);
            job.Entities = state.GetEntityTypeHandle();
            job.Functionals = state.GetComponentTypeHandle<FunctionalBlock>(true);
            job.Mounts = state.GetComponentTypeHandle<WeaponMount>(true);
            job.Aims = state.GetComponentTypeHandle<WeaponAim>(true);
            job.States = state.GetComponentTypeHandle<WeaponState>(false);
            job.Commands = state.GetComponentLookup<ShipCommand>(true);
            job.Transforms = state.GetComponentLookup<LocalTransform>(true);
            job.Velocities = state.GetComponentLookup<PhysicsVelocity>(true);
            job.Owners = state.GetComponentLookup<OwnedBy>(true);
            job.Players = state.GetComponentLookup<PlayerShip>(true);
            job.Ids = state.GetComponentLookup<PersistentId>(true);
            job.Controls = state.GetComponentLookup<WeaponControl>(true);
            job.Targets = state.GetComponentLookup<ShipTarget>(true);
            job.Allegiances = state.GetComponentLookup<Allegiance>(true);
            job.Multipliers = state.GetComponentLookup<ShipStatMultipliers>(true);
            job.Power = state.GetComponentLookup<Powered>(true);
            job.Beams = state.GetComponentLookup<BeamState>(true);
            job.Hulks = state.GetComponentLookup<Hulk>(true);
            job.Dormant = state.GetComponentLookup<Dormant>(true);
            job.Spawning = state.GetComponentLookup<GridSpawning>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WeaponDefsRef>(); state.RequireForUpdate<SimClock>();
            state.RequireForUpdate<WeaponTuning>(); state.RequireForUpdate<ProjectileStore>();
            state.RequireForUpdate<FxEventQueue>(); state.RequireForUpdate<SfxEventQueue>(); state.RequireForUpdate<FactionRelations>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) { state.CompleteDependency(); owned.Access.Dispose(); owned.Shots.Dispose(); }
        [BurstCompile] public void OnStartRunning(ref SystemState state) { owned.Access.CompleteAccess(); owned.Shots.Clear(); }
        [BurstCompile] public void OnStopRunning(ref SystemState state) { owned.Access.CompleteAccess(); owned.Shots.Clear(); }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Entities.Update(ref state);
            job.Functionals.Update(ref state);
            job.Mounts.Update(ref state);
            job.Aims.Update(ref state);
            job.States.Update(ref state);
            job.Commands.Update(ref state);
            job.Transforms.Update(ref state);
            job.Velocities.Update(ref state);
            job.Owners.Update(ref state);
            job.Players.Update(ref state);
            job.Ids.Update(ref state);
            job.Controls.Update(ref state);
            job.Targets.Update(ref state);
            job.Allegiances.Update(ref state);
            job.Multipliers.Update(ref state);
            job.Power.Update(ref state);
            job.Beams.Update(ref state);
            job.Hulks.Update(ref state);
            job.Dormant.Update(ref state);
            job.Spawning.Update(ref state);
            var stream = new NativeStream(math.max(1, query.CalculateChunkCountWithoutFiltering()), Allocator.TempJob);
            job.Shots = stream.AsWriter(); job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value;
            job.Tick = SystemAPI.GetSingleton<SimClock>().Tick; job.Relations = SystemAPI.GetSingleton<FactionRelations>();
            var fx = SystemAPI.GetSingleton<FxEventQueue>(); var sfx = SystemAPI.GetSingleton<SfxEventQueue>();
            job.Fx = fx.Queue.AsParallelWriter(); job.Sfx = sfx.Queue.AsParallelWriter();
            var firing = job.ScheduleParallel(query, sfx.Access.BeginRead(fx.Access.BeginRead(state.Dependency)));
            fx.Access.EndRead(firing); sfx.Access.EndRead(firing);
            var flattened = new ShotFlattenJob { Reader = stream.AsReader(), Shots = owned.Shots }.Schedule(owned.Access.Acquire(firing));
            owned.Access.Release(flattened); state.Dependency = stream.Dispose(flattened);
        }
    }
}
