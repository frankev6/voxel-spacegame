using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Voxel.Grid;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Voxel.Damage
{
    /// <summary>FND-020 / ADR-0053: exact changed-grid connectivity; DMG-008 caches brick connectivity; DMG-009 adds the hybrid race traversal.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(DamageResolveSystem)), UpdateBefore(typeof(GridMassUpdateSystem))]
    public unsafe partial struct IntegritySystem : ISystem
    {
        ConnectivityJob job; EntityQuery grids;
        NativeParallelHashSet<Entity> sizeWarnings;
        static readonly ProfilerMarker Race = new ProfilerMarker(ProfilerCategory.Scripts, "IntegritySystem.Race");
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            sizeWarnings = new NativeParallelHashSet<Entity>(64, Allocator.Persistent);
            grids = new EntityQueryBuilder(Allocator.Temp).WithAll<VoxelGrid, IntegritySeed>().Build(ref state);
            job.Seeds = state.GetBufferLookup<IntegritySeed>();
            job.Summaries = state.GetComponentLookup<BrickIntegrity>();
            job.Reader = GridCellReader.Create(ref state); job.Instances = state.GetBufferLookup<BlockInstance>(true);
            job.Palettes = state.GetBufferLookup<PaletteEntry>(true); job.Bricks = state.GetComponentLookup<Brick>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<BlockDefsRef>();
            state.RequireForUpdate<OrientationTablesRef>(); state.RequireForUpdate<DamageConfig>();
        }
        [BurstCompile] public void OnDestroy(ref SystemState state) { state.CompleteDependency(); sizeWarnings.Dispose(); }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            using var marker = Race.Auto(); job.Reader.Update(ref state); job.Instances.Update(ref state); job.Palettes.Update(ref state); job.Bricks.Update(ref state);
            job.Seeds.Update(ref state); job.Summaries.Update(ref state);
            job.Roots = grids.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var gather);
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, gather);
            var data = SystemAPI.GetSingleton<DamageStreams>().Data;
            job.Touched = data->TouchedBricks; job.WarmupBudget = SystemAPI.GetSingleton<DamageConfig>().BrickWarmupPerTick;
            job.Removed = data->Removed; job.Ranges = data->RemovedGrids; job.Islands = data->Islands; job.Masks = data->IslandMasks;
            job.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value; job.Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value;
            job.Stats = data; job.SizeWarnings = sizeWarnings; job.CellCap = SystemAPI.GetSingleton<DamageConfig>().MaxFullFloodCells;
            state.Dependency = job.Schedule(JobHandle.CombineDependencies(state.Dependency, data->ResolverHandle)); data->IntegrityHandle = state.Dependency;
        }
        [BurstCompile] struct ConnectivityJob : IJob
        {
            public GridCellReader Reader;
            public int CellCap, WarmupBudget;
            public ComponentLookup<BrickIntegrity> Summaries;
            [ReadOnly] public NativeList<TouchedBrick> Touched;
            public NativeParallelHashSet<Entity> SizeWarnings;
            [ReadOnly] public NativeList<Entity> Roots;
            public BufferLookup<IntegritySeed> Seeds;
            [ReadOnly] public BufferLookup<BlockInstance> Instances;
            [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
            [ReadOnly] public ComponentLookup<Brick> Bricks;
            [ReadOnly] public NativeList<CellRemoved> Removed;
            [ReadOnly] public NativeList<GridRemovedRange> Ranges;
            public BlobAssetReference<BlockDefsBlob> Blocks;
            public BlobAssetReference<OrientationTablesBlob> Orientations;
            public NativeList<IslandRecord> Islands;
            public NativeList<IslandMask> Masks;
            [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Stats;
            struct Node : System.IComparable<Node>
            {
                public int3 Position; public Cell Cell;
                public int CompareTo(Node b)
                { int c = Position.z.CompareTo(b.Position.z); if (c == 0) c = Position.y.CompareTo(b.Position.y); return c == 0 ? Position.x.CompareTo(b.Position.x) : c; }
            }
            struct Bucket : System.IComparable<Bucket>
            {
                public int Label, Slot; public CellMask Mask;
                public int CompareTo(Bucket b) { int c = Label.CompareTo(b.Label); return c == 0 ? Slot.CompareTo(b.Slot) : c; }
            }
            struct Component { public int Count; public int3 Min, Max; public bool Anchored, Functional; }
            public void Execute()
            {
                Islands.Clear(); Masks.Clear();
                using var changed = new NativeList<Entity>(Allocator.Temp);
                foreach (var root in Roots) if (Seeds[root].Length > 0) { changed.Add(root); }
                foreach (var range in Ranges)
                {
                    for (int i = range.Start; i < range.Start + range.Count; i++)
                        if (Removed[i].Reason == CellRemovedReason.Destroyed) { changed.Add(range.Grid); if (Seeds[range.Grid].Length == 0) Seeds[range.Grid].Add(new IntegritySeed { Cell = Removed[i].Cell }); break; }
                }
                changed.Sort(new RootOrder());
                int count = 0;
                for (int i = 0; i < changed.Length; i++)
                    if ((i == 0 || changed[i] != changed[i - 1]) && Reader.Grids.HasComponent(changed[i])) count++;
                int ordinal = 0;
                for (int i = 0; i < changed.Length; i++)
                    if ((i == 0 || changed[i] != changed[i - 1]) && Reader.Grids.HasComponent(changed[i]))
                    {
                        int share = math.max(0, WarmupBudget) / count + (ordinal++ < math.max(0, WarmupBudget) % count ? 1 : 0);
                        CheckBricks(changed[i], share); Find(changed[i]);
                    }
            }
            struct RootOrder : System.Collections.Generic.IComparer<Entity>
            { public int Compare(Entity a, Entity b) => a.Index.CompareTo(b.Index); }
            void CheckBricks(Entity root, int warmup)
            {
                foreach (var touched in Touched)
                    if (touched.Grid == root && Summaries.HasComponent(touched.Brick) && Bricks[touched.Brick].SolidCount != 0)
                        Check(touched.Brick);
                var slots = Reader.Slots[root];
                for (int slot = 0; slot < slots.Length && warmup > 0; slot++)
                {
                    var brick = slots[slot].Brick;
                    if (brick == Entity.Null || !Summaries.HasComponent(brick) || Bricks[brick].SolidCount == 0) continue;
                    if (Summaries[brick].State != BrickConnectivity.Unknown) continue;
                    Check(brick); warmup--;
                }
            }
            void Check(Entity brick) => Summaries[brick] = BrickConnectivityCheck.Evaluate(Bricks[brick].SolidCount,
                Reader.Masks[brick], Reader.Cells[brick], Reader.Sparse[brick]);
            void Find(Entity root)
            {
                Stats->Current.IntegrityGrids++;
                var grid = Reader.Grids[root]; var slots = Reader.Slots[root]; var instances = Instances[root]; var palette = Palettes[root];
                // TODO(DMG-009): hybrid race traversal, node budgets and force-after-deferral semantics.
                long occupied = 0;
                foreach (var slot in slots) if (slot.Brick != Entity.Null) occupied += Bricks[slot.Brick].SolidCount;
                if (occupied > CellCap)
                {
                    var seeds = Seeds[root];
                    if (seeds.Length == 0) seeds.Add(new IntegritySeed { Cell = grid.BrickMin * 16 });
                    Stats->Current.IntegritySizeDeferred++;
                    if (SizeWarnings.Add(root))
                    {
                        // Character appends avoid a managed string conversion inside Burst (SPEC-13 Q22).
                        var warning = default(FixedString128Bytes);
                        warning.Append('['); warning.Append('D'); warning.Append('a'); warning.Append('m'); warning.Append('a'); warning.Append('g'); warning.Append('e'); warning.Append(']');
                        warning.Append(' '); warning.Append('f'); warning.Append('l'); warning.Append('o'); warning.Append('o'); warning.Append('d'); warning.Append(' '); warning.Append('d');
                        warning.Append('e'); warning.Append('f'); warning.Append('e'); warning.Append('r'); warning.Append('r'); warning.Append('e'); warning.Append('d'); warning.Append(';');
                        warning.Append(' '); warning.Append('s'); warning.Append('e'); warning.Append('e'); warning.Append('d'); warning.Append('s'); warning.Append(' '); warning.Append('k');
                        warning.Append('e'); warning.Append('p'); warning.Append('t'); warning.Append('.'); warning.Append(' '); warning.Append('c'); warning.Append('a'); warning.Append('p');
                        warning.Append('=');
                        warning.Append(CellCap); Game.Core.GameLog.Burst.Warn(warning);
                    }
                    return;
                }
                Seeds[root].Clear();
                using var nodes = new NativeList<Node>(Allocator.Temp);
                foreach (var slot in slots)
                {
                    if (slot.Brick == Entity.Null) continue;
                    var mask = Reader.Masks[slot.Brick]; var brick = Bricks[slot.Brick];
                    var dense = Reader.Cells[slot.Brick]; var sparse = Reader.Sparse[slot.Brick];
                    for (int local = 0; local < 4096; local++)
                        if ((mask[local >> 6].Bits & (1UL << (local & 63))) != 0)
                            nodes.Add(new Node { Position = BrickMath.CellOf(brick.Coord, local), Cell = BrickWrite.Get(dense, sparse, local) });
                }
                if (nodes.Length == 0) return;
                nodes.Sort();
                using var lookup = new NativeHashMap<int3, int>(nodes.Length, Allocator.Temp);
                var labels = new NativeArray<int>(nodes.Length, Allocator.Temp);
                using var components = new NativeList<Component>(Allocator.Temp);
                using var queue = new NativeList<int>(Allocator.Temp);
                for (int i = 0; i < nodes.Length; i++) lookup.Add(nodes[i].Position, i);
                for (int seed = 0; seed < nodes.Length; seed++)
                {
                    if (labels[seed] != 0) continue;
                    int label = components.Length + 1; labels[seed] = label; queue.Clear(); queue.Add(seed);
                    var component = new Component { Min = new int3(int.MaxValue), Max = new int3(int.MinValue) };
                    for (int head = 0; head < queue.Length; head++)
                    {
                        var node = nodes[queue[head]]; component.Count++; component.Min = math.min(component.Min, node.Position); component.Max = math.max(component.Max, node.Position);
                        for (int face = 0; face < 6; face++) Visit(node.Position + BrickMath.FaceDirection((BlockFace)face), label, lookup, labels, queue);
                        ushort block = node.Cell.IsInstance ? instances[node.Cell.InstanceIndex].Block : palette[node.Cell.PaletteIndex].Block;
                        ref var def = ref Blocks.Value.Blocks[block];
                        if (node.Cell.IsInstance)
                        {
                            var instance = instances[node.Cell.InstanceIndex];
                            component.Anchored |= node.Cell.InstanceIndex == grid.AnchorInstance;
                            component.Functional |= def.Kind == BlockKind.Functional;
                            for (int c = 0; c < def.Cells.Length; c++)
                            {
                                int3 p = instance.Origin + Orientations.Value.Rotate(def.Cells[c], instance.Orient);
                                if (lookup.TryGetValue(p, out int other) && nodes[other].Cell.Ref == node.Cell.Ref) Visit(p, label, lookup, labels, queue);
                            }
                        }
                    }
                    components.Add(component);
                }
                int anchor = 0;
                for (int i = 0; i < components.Length; i++)
                    if (components[i].Anchored) { anchor = i; break; }
                    else if (components[i].Count > components[anchor].Count) anchor = i;
                // Aggregate once per occupied node; sorted buckets preserve component/slot emission order.
                using var bucketIndex = new NativeHashMap<int2, int>(nodes.Length, Allocator.Temp);
                using var buckets = new NativeList<Bucket>(Allocator.Temp);
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (labels[i] == anchor + 1) continue;
                    int slot = BrickMath.SlotIndex(BrickMath.BrickOf(nodes[i].Position), grid.BrickMin, grid.BrickDims);
                    var key = new int2(labels[i], slot);
                    if (!bucketIndex.TryGetValue(key, out int index))
                    {
                        index = buckets.Length; bucketIndex.Add(key, index);
                        buckets.Add(new Bucket { Label = labels[i], Slot = slot });
                    }
                    int local = BrickMath.LocalIndex(BrickMath.LocalOf(nodes[i].Position));
                    ref var bucket = ref buckets.ElementAt(index); bucket.Mask.Words[local >> 6] |= 1UL << (local & 63);
                }
                buckets.Sort(); int next = 0;
                for (int component = 0; component < components.Length; component++)
                {
                    if (component == anchor) continue;
                    var description = components[component]; int start = Masks.Length;
                    while (next < buckets.Length && buckets[next].Label == component + 1)
                    {
                        var bucket = buckets[next++]; Masks.Add(new IslandMask { SlotIndex = bucket.Slot, Mask = bucket.Mask });
                    }
                    Islands.Add(new IslandRecord { Grid = root, MaskStart = start, MaskCount = Masks.Length - start, CellCount = description.Count,
                        CellMin = description.Min, CellMax = description.Max, HasFunctional = description.Functional ? (byte)1 : (byte)0, Ordinal = component });
                    Stats->Current.IslandsFound++;
                }
                labels.Dispose();
            }
            static void Visit(int3 p, int label, NativeHashMap<int3, int> lookup, NativeArray<int> labels, NativeList<int> queue)
            {
                if (!lookup.TryGetValue(p, out int i) || labels[i] != 0) return;
                labels[i] = label; queue.Add(i);
            }
        }
    }
}
