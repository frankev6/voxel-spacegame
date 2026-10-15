using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.6: the tick's ordered, deduplicated structural destruction (emptied bricks, functional entities of lost blocks,
    /// dissolved grids) and the cleanup of surviving roots' LinkedEntityGroups. ADR-0117 applier: every machine runs it, whoever decided.</summary>
    [BurstCompile] internal struct StructuralDestroyJob : IJob
    {
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        public BufferLookup<LinkedEntityGroup> Groups;
        [ReadOnly] public EntityStorageInfoLookup Storage;
        [ReadOnly] public NativeList<Entity> Destroy;
        [ReadOnly] public NativeList<Entity> Despawns;
        [ReadOnly] public NativeList<DamageGridResult> Results;
        public EntityCommandBuffer Ecb;
        public void Execute()
        {
            using var ordered = new NativeList<Entity>(Allocator.Temp); ordered.AddRange(Destroy.AsArray()); ordered.AddRange(Despawns.AsArray()); ordered.Sort(new RootOrder());
            // Root destruction recursively owns its current LEG. Suppress separate child commands
            // before editing surviving groups; duplicate root requests and stale members are harmless.
            var covered = new NativeParallelHashSet<Entity>(math.max(1, ordered.Length), Allocator.Temp);
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
            covered.Dispose();
        }
        struct RootOrder : System.Collections.Generic.IComparer<Entity>
        { public int Compare(Entity a, Entity b) { int c = a.Index.CompareTo(b.Index); return c == 0 ? a.Version.CompareTo(b.Version) : c; } }
    }
}
