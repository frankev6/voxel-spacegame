using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Streams;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.6: core-loss transition and the final, deduplicated structural destruction pass.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(SplitSystem))]
    [UpdateBefore(typeof(GridMassUpdateSystem)), UpdateBefore(typeof(BrickDirtyScheduleSystem))]
    public unsafe partial struct ShipDeathSystem : ISystem
    {
        DeathJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Grids = state.GetComponentLookup<VoxelGrid>(); job.Hulks = state.GetComponentLookup<Hulk>(true);
            job.Ids = state.GetComponentLookup<PersistentId>(true); job.Groups = state.GetBufferLookup<LinkedEntityGroup>();
            job.Storage = state.GetEntityStorageInfoLookup();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate<GameplayEndCommandBufferSystem.Singleton>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Grids.Update(ref state); job.Hulks.Update(ref state); job.Ids.Update(ref state); job.Groups.Update(ref state); job.Storage.Update(ref state);
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var data = streams.Data;
            job.Destroy = data->DestroyList; job.Results = data->GridResults; job.CoreLost = data->CoreLost;
            job.Config = SystemAPI.GetSingleton<DamageConfig>(); job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            job.Explosions = streams.Explosions.BeginWrite(ref state, ProducerKind.Explosion, 1);
            job.Ecb = SystemAPI.GetSingleton<GameplayEndCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            state.Dependency = job.Schedule(JobHandle.CombineDependencies(state.Dependency, data->SplitHandle, data->ResolverHandle));
            streams.Explosions.EndWrite(state.Dependency); data->DeathHandle = state.Dependency; data->CoreLostWriteHandle = state.Dependency;
        }
        [BurstCompile] struct DeathJob : IJob
        {
            public ComponentLookup<VoxelGrid> Grids;
            [ReadOnly] public ComponentLookup<Hulk> Hulks;
            [ReadOnly] public ComponentLookup<PersistentId> Ids;
            public BufferLookup<LinkedEntityGroup> Groups;
            [ReadOnly] public EntityStorageInfoLookup Storage;
            [ReadOnly] public NativeList<Entity> Destroy;
            [ReadOnly] public NativeList<DamageGridResult> Results;
            public NativeList<GridCoreLost> CoreLost;
            public NativeStream.Writer Explosions;
            public EntityCommandBuffer Ecb;
            public DamageConfig Config; public uint Tick;
            public void Execute()
            {
                CoreLost.Clear(); Explosions.BeginForEachIndex(0);
                foreach (var result in Results)
                {
                    Entity root = result.Grid;
                    if (result.CoreLost == 0 || Hulks.HasComponent(root) || !Grids.HasComponent(root)) continue;
                    var grid = Grids[root]; var id = Ids[root].Value;
                    Ecb.AddComponent<Hulk>(root); Ecb.AddComponent(root, new HulkInfo { Cause = HulkCause.CoreLost, Tick = Tick, OriginPersistentId = id, FormerKind = grid.Kind });
                    grid.Kind = GridKind.Hulk; Grids[root] = grid;
                    CoreLost.Add(new GridCoreLost { Grid = root, PersistentId = id, Tick = Tick, WorldPos = result.CoreWorldPos });
                    Explosions.Write(new ExplosionRequest { WorldPos = result.CoreWorldPos, Radius = Config.CoreBreachRadiusCells * grid.CellSize,
                        Amount = Config.CoreBreachDamage, Type = DamageType.Explosive, Source = root, Producer = ProducerKind.Explosion });
                }
                Explosions.EndForEachIndex();
                using var ordered = new NativeList<Entity>(Allocator.Temp); ordered.AddRange(Destroy.AsArray()); ordered.Sort(new RootOrder());
                // Remove dead children from surviving LEGs. Split's earlier ECB has its own filtered copy,
                // so filter that copy there too; this direct cleanup protects roots without a split this tick.
                foreach (var result in Results)
                {
                    if (!Groups.HasBuffer(result.Grid)) continue;
                    var group = Groups[result.Grid];
                    for (int i = group.Length - 1; i > 0; i--)
                        if (!Storage.Exists(group[i].Value) || ordered.Contains(group[i].Value)) group.RemoveAt(i);
                }
                for (int i = 0; i < ordered.Length; i++)
                    if ((i == 0 || ordered[i] != ordered[i - 1]) && Storage.Exists(ordered[i])) Ecb.DestroyEntity(ordered[i]);
            }
            struct RootOrder : System.Collections.Generic.IComparer<Entity>
            { public int Compare(Entity a, Entity b) { int c = a.Index.CompareTo(b.Index); return c == 0 ? a.Version.CompareTo(b.Version) : c; } }
        }
    }
}
