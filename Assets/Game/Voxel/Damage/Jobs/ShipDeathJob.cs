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
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.6: core loss and ordered final destruction after split recording.</summary>
    [BurstCompile] internal struct ShipDeathJob : IJob
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
            // Root destruction recursively owns its current LEG. Suppress separate child commands
            // before editing surviving groups; duplicate root requests and stale members are harmless.
            using var covered = new NativeParallelHashSet<Entity>(math.max(1, ordered.Length), Allocator.Temp);
            foreach (var root in ordered)
            {
                if (!Storage.Exists(root) || !Grids.HasComponent(root) || !Groups.HasBuffer(root)) continue;
                var members = Groups[root];
                for (int i = 0; i < members.Length; i++)
                {
                    Entity member = members[i].Value;
                    if (member == root) continue;
                    if (covered.Count() == covered.Capacity) covered.Capacity = math.max(1, covered.Capacity * 2);
                    covered.Add(member);
                }
            }
            // Remove dead children from surviving LEGs. Split's earlier ECB has its own filtered copy,
            // so filter that copy there too; this direct cleanup protects roots without a split this tick.
            foreach (var result in Results)
            {
                if (ordered.Contains(result.Grid) || !Groups.HasBuffer(result.Grid)) continue;
                var group = Groups[result.Grid];
                for (int i = group.Length - 1; i > 0; i--)
                    if (!Storage.Exists(group[i].Value) || ordered.Contains(group[i].Value)) group.RemoveAt(i);
            }
            for (int i = 0; i < ordered.Length; i++)
                if ((i == 0 || ordered[i] != ordered[i - 1]) && Storage.Exists(ordered[i]) && !covered.Contains(ordered[i])) Ecb.DestroyEntity(ordered[i]);
        }
        struct RootOrder : System.Collections.Generic.IComparer<Entity>
        { public int Compare(Entity a, Entity b) { int c = a.Index.CompareTo(b.Index); return c == 0 ? a.Version.CompareTo(b.Version) : c; } }
    }
}
