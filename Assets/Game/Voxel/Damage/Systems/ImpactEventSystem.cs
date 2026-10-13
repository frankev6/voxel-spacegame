using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.2: Unity Physics contacts become ram damage; prior-tick damage becomes impulses.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(DamageResolveSystem))]
    public unsafe partial struct ImpactEventSystem : ISystem
    {
        NativeList<RamContact> contacts;
        ComponentLookup<PlanarConstraint> planar;
        EmitRamDamageJob emit;
        static readonly ProfilerMarker Apply = new ProfilerMarker(ProfilerCategory.Scripts, "ImpactEventSystem.ApplyImpulses");
        static readonly ProfilerMarker Collect = new ProfilerMarker(ProfilerCategory.Scripts, "ImpactEventSystem.Collect");
        static readonly ProfilerMarker Emit = new ProfilerMarker(ProfilerCategory.Scripts, "ImpactEventSystem.Emit");
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            contacts = new NativeList<RamContact>(64, Allocator.Persistent);
            planar = state.GetComponentLookup<PlanarConstraint>(true);
            emit.Cells = GridCellReader.Create(ref state); emit.Palettes = state.GetBufferLookup<PaletteEntry>(true); emit.Instances = state.GetBufferLookup<BlockInstance>(true);
            emit.Transforms = state.GetComponentLookup<LocalTransform>(true); emit.Masses = state.GetComponentLookup<PhysicsMass>(true);
            emit.Velocities = state.GetComponentLookup<PhysicsVelocity>(true); emit.Owners = state.GetComponentLookup<OwnedBy>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>(); state.RequireForUpdate<SimClock>();
            state.RequireForUpdate<PhysicsWorldSingleton>(); state.RequireForUpdate<SimulationSingleton>(); state.RequireForUpdate<BlockDefsRef>();
            state.RequireForUpdate<FxEventQueue>(); state.RequireForUpdate<SfxEventQueue>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) { state.CompleteDependency(); contacts.Dispose(); }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var data = streams.Data; uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            state.Dependency = data->AllJobs(state.Dependency); planar.Update(ref state);
            using (Apply.Auto()) state.Dependency = new ApplyPendingImpulseJob { Tick = tick, Planar = planar }.ScheduleParallel(state.Dependency);
            // TODO(DMG-019): consume and apply the separate ImpulseRequest channel after pending damage impulses.
            emit.Cells.Update(ref state); emit.Palettes.Update(ref state); emit.Instances.Update(ref state); emit.Transforms.Update(ref state);
            emit.Masses.Update(ref state); emit.Velocities.Update(ref state); emit.Owners.Update(ref state);
            state.Dependency = new ClearContactsJob { Contacts = contacts }.Schedule(state.Dependency);
            using (Collect.Auto()) state.Dependency = new CollectRamContactsJob
            {
                Contacts = contacts, Grids = emit.Cells.Grids, World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
            emit.Contacts = contacts; emit.Pairs = data->RamPairs; emit.Config = SystemAPI.GetSingleton<DamageConfig>();
            emit.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value; emit.Tick = tick; emit.Data = data;
            emit.Damage = streams.Damage.BeginWrite(ref state, ProducerKind.Impact, 1);
            var fx = SystemAPI.GetSingleton<FxEventQueue>(); emit.Fx = fx.Queue.AsParallelWriter();
            var sfx = SystemAPI.GetSingleton<SfxEventQueue>(); emit.Sfx = sfx.Queue.AsParallelWriter();
            using (Emit.Auto()) state.Dependency = emit.Schedule(sfx.Access.BeginRead(fx.Access.BeginRead(state.Dependency)));
            sfx.Access.EndRead(state.Dependency); fx.Access.EndRead(state.Dependency); streams.Damage.EndWrite(state.Dependency); data->ImpactHandle = state.Dependency;
        }
        [BurstCompile] struct ClearContactsJob : IJob
        { public NativeList<RamContact> Contacts; public void Execute() => Contacts.Clear(); }
    }
}
