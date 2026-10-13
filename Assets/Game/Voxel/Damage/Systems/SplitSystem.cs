using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Voxel.Grid;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.PresentationStreams;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.5: ordered particle, chunk and hulk split recording.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(IntegritySystem)), UpdateBefore(typeof(GridMassUpdateSystem)), UpdateBefore(typeof(BrickDirtyScheduleSystem))]
    public unsafe partial struct SplitSystem : ISystem
    {
        RecordHulkJob job;
        EntityArchetype chunkType;
        ComponentLookup<ChunkColliderBlob> chunkColliders;
        BufferLookup<ChunkCell> chunkCells;
        public void OnCreate(ref SystemState state)
        {
            job.RootType = state.EntityManager.CreateArchetype(typeof(VoxelGrid), typeof(BrickSlot), typeof(PaletteEntry), typeof(BlockInstance),
                typeof(LocalTransform), typeof(LocalToWorld), typeof(LinkedEntityGroup), typeof(PersistentId), typeof(GridSource), typeof(GridSpawning),
                typeof(GridDespawnRequest), typeof(GridRebuilt), typeof(GridBodyInit), typeof(GridEditRequest), typeof(GridEditedBrick), typeof(GridSpawnBrick),
                typeof(GridMass), typeof(GridMassRecompute), typeof(GridColliderState), typeof(Dormant), typeof(SpawnReadToken),
                typeof(GridDamageStats), typeof(PendingImpulse), typeof(GridIntegrityState), typeof(IntegritySeed));
            chunkType = state.EntityManager.CreateArchetype(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsVelocity),
                typeof(PhysicsMass), typeof(PhysicsCollider), typeof(PhysicsDamping), typeof(PhysicsWorldIndex), typeof(PhysicsGraphicalSmoothing),
                typeof(PhysicsGraphicalInterpolationBuffer), typeof(ChunkBody), typeof(ChunkContactState), typeof(ChunkCell), typeof(ChunkColliderBlob),
                typeof(SplitGrace), typeof(SplitOrigin), typeof(ChunkDissolve), typeof(Disabled));
            chunkColliders = state.GetComponentLookup<ChunkColliderBlob>(true); chunkCells = state.GetBufferLookup<ChunkCell>(true);
            job.Entities = state.GetEntityStorageInfoLookup();
            job.Reader = GridCellReader.Create(ref state);
            job.Transforms = state.GetComponentLookup<LocalTransform>(true); job.Masses = state.GetComponentLookup<PhysicsMass>(true);
            job.Velocities = state.GetComponentLookup<PhysicsVelocity>(true); job.Damping = state.GetComponentLookup<PhysicsDamping>(true);
            job.Planar = state.GetComponentLookup<PlanarConstraint>(true); job.Ids = state.GetComponentLookup<PersistentId>(true);
            job.Sources = state.GetComponentLookup<GridSource>(true); job.Bricks = state.GetComponentLookup<Brick>(true);
            job.Instances = state.GetBufferLookup<BlockInstance>(true); job.Palettes = state.GetBufferLookup<PaletteEntry>(true);
            job.Groups = state.GetBufferLookup<LinkedEntityGroup>(true); job.Seeds = state.GetBufferLookup<IntegritySeed>();
            job.Allocators = state.GetComponentLookup<PersistentIdAllocator>(); job.Serials = state.GetComponentLookup<GridSerialAllocator>();
            state.RequireForUpdate<FxEventQueue>(); state.RequireForUpdate<SfxEventQueue>();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<PhysicsVoxelConfig>(); state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<PersistentIdAllocator>();
            state.RequireForUpdate<GridPrototypes>(); state.RequireForUpdate<GridSerialAllocator>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<GameplayEndCommandBufferSystem.Singleton>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            chunkColliders.Update(ref state); chunkCells.Update(ref state);
            job.Entities.Update(ref state); job.Reader.Update(ref state); job.Transforms.Update(ref state); job.Masses.Update(ref state); job.Velocities.Update(ref state);
            job.Damping.Update(ref state); job.Planar.Update(ref state); job.Ids.Update(ref state); job.Sources.Update(ref state); job.Bricks.Update(ref state);
            job.Instances.Update(ref state); job.Palettes.Update(ref state); job.Groups.Update(ref state); job.Seeds.Update(ref state);
            job.Allocators.Update(ref state); job.Serials.Update(ref state);
            var data = SystemAPI.GetSingleton<DamageStreams>().Data;
            job.Islands = data->Islands; job.Masks = data->IslandMasks; job.Destroy = data->DestroyList; job.Detach = data->PendingDetachNext; job.Stats = data;
            job.Config = SystemAPI.GetSingleton<DamageConfig>(); job.PhysicsConfig = SystemAPI.GetSingleton<PhysicsVoxelConfig>();
            job.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value; job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            job.AllocatorEntity = SystemAPI.GetSingletonEntity<PersistentIdAllocator>(); job.SerialEntity = SystemAPI.GetSingletonEntity<GridSerialAllocator>(); job.BrickPrototype = SystemAPI.GetSingleton<GridPrototypes>().Brick;
            job.Ecb = SystemAPI.GetSingleton<GameplayEndCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var bodies = new NativeList<IslandRecord>(Allocator.TempJob);
            var chunks = new NativeList<IslandRecord>(Allocator.TempJob);
            var slots = new NativeArray<Entity>(math.clamp(job.Config.MaxLiveChunks, 1, 256), Allocator.TempJob);
            var chunkBodies = new NativeArray<ChunkBody>(slots.Length, Allocator.TempJob);
            var dependency = JobHandle.CombineDependencies(state.Dependency, data->IntegrityHandle, data->DetachNextWritable);
            var fx = SystemAPI.GetSingleton<FxEventQueue>();
            var sfx = SystemAPI.GetSingleton<SfxEventQueue>();
            dependency = new EmitDebrisJob { Removed = data->Removed, Ranges = data->RemovedGrids, Grids = job.Reader.Grids, Blocks = job.Blocks,
                Config = job.Config, Fx = fx.Queue.AsParallelWriter(), Sfx = sfx.Queue.AsParallelWriter(), Data = data
            }.Schedule(sfx.Access.BeginRead(fx.Access.BeginRead(dependency)));
            sfx.Access.EndRead(dependency);
            dependency = new ChunkLifetimeJob { Tick = job.Tick, Ecb = job.Ecb, Retired = data->RetiredChunkBlobs,
                Fx = fx.Queue.AsParallelWriter() }.Schedule(dependency);
            dependency = new CollectChunkSlotsJob { Entities = slots, Bodies = chunkBodies }.Schedule(dependency);
            dependency = new PlanSplitJob { Islands = data->Islands, Masks = data->IslandMasks, Reader = job.Reader, Bricks = job.Bricks,
                Seeds = job.Seeds, Allocators = job.Allocators, AllocatorEntity = job.AllocatorEntity, HulkAvailable = job.BrickPrototype != Entity.Null,
                Config = job.Config, Bodies = bodies, Chunks = chunks, Detach = data->PendingDetachNext, Data = data }.Schedule(dependency);
            job.Islands = bodies;
            dependency = new RecordChunkJob { Islands = chunks, Masks = data->IslandMasks, Reader = job.Reader, Bricks = job.Bricks,
                Transforms = job.Transforms, Masses = job.Masses, Velocities = job.Velocities, Colliders = chunkColliders,
                OldCells = chunkCells, Palettes = job.Palettes, Instances = job.Instances, Slots = slots, Bodies = chunkBodies, Retired = data->RetiredChunkBlobs,
                Archetype = chunkType, Blocks = job.Blocks, Config = job.Config, Tick = job.Tick, Ecb = job.Ecb,
                Fx = fx.Queue.AsParallelWriter(), Data = data }.Schedule(dependency);
            dependency = job.Schedule(dependency);
            fx.Access.EndRead(dependency);
            dependency = bodies.Dispose(dependency); dependency = chunks.Dispose(dependency);
            dependency = slots.Dispose(dependency); state.Dependency = chunkBodies.Dispose(dependency);
            data->SplitHandle = state.Dependency;
        }

    }
}
