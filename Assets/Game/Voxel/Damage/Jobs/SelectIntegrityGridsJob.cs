using Game.Data.Blocks;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    internal struct IntegrityResult { public int Nodes, Islands, Deferred; }
    [BurstCompile] internal struct SelectIntegrityGridsJob : IJob
    {
        [ReadOnly] public NativeList<Entity> Roots;
        [ReadOnly] public NativeList<CellRemoved> Removed;
        [ReadOnly] public NativeList<GridRemovedRange> Ranges;
        public GridCellReader Reader;
        public BufferLookup<IntegritySeed> Seeds;
        public NativeList<Entity> Candidates;
        public NativeList<IntegrityResult> Results;
        public NativeReference<int> Deferred;
        public int Limit;
        public void Execute()
        {
            Candidates.Clear(); Results.Clear(); Deferred.Value = 0;
            using var ordered = new NativeList<Entity>(Allocator.Temp);
            foreach (var root in Roots) ordered.Add(root);
            ordered.Sort(new EntityOrder());
            foreach (var root in ordered)
            {
                using var positions = new NativeList<int3>(Allocator.Temp);
                using var seen = new NativeHashSet<int3>(16, Allocator.Temp);
                foreach (var range in Ranges)
                    if (range.Grid == root)
                        for (int i = range.Start; i < range.Start + range.Count; i++)
                        {
                            var removed = Removed[i]; if (removed.Reason != CellRemovedReason.Destroyed) continue;
                            for (int face = 0; face < 6; face++) Add(root, removed.Cell + BrickMath.FaceDirection((BlockFace)face), positions, seen);
                        }
                var seeds = Seeds[root];
                foreach (var seed in seeds) Add(root, seed.Cell, positions, seen);
                seeds.Clear(); foreach (var p in positions) seeds.Add(new IntegritySeed { Cell = p });
                if (positions.Length == 0) continue;
                if (Candidates.Length < math.max(0, Limit)) { Candidates.Add(root); Results.Add(default); }
                else Deferred.Value++;
            }
        }
        void Add(Entity root, int3 p, NativeList<int3> positions, NativeHashSet<int3> seen)
        { if (Reader.IsSolid(root, p) && seen.Add(p)) positions.Add(p); }
        struct EntityOrder : System.Collections.Generic.IComparer<Entity>
        { public int Compare(Entity a, Entity b) => a.Index.CompareTo(b.Index); }
    }
}
