using Game.Data.Blocks;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>One exclusive grid per worker. Only this grid's seeds, state and brick summaries are written.</summary>
    [BurstCompile] internal unsafe struct IntegrityRaceJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<Entity> Candidates;
        public NativeArray<IntegrityResult> Results;
        public NativeStream.Writer Output;
        public GridCellReader Reader;
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public NativeList<TouchedBrick> Touched;
        [NativeDisableParallelForRestriction] public ComponentLookup<BrickIntegrity> Summaries;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridIntegrityState> States;
        [NativeDisableParallelForRestriction] public BufferLookup<IntegritySeed> Seeds;
        public BlobAssetReference<BlockDefsBlob> Blocks;
        public BlobAssetReference<OrientationTablesBlob> Orientations;
        public DamageConfig Config;
        public void Execute(int index)
        {
            Entity grid = Candidates[index];
            int quota = math.max(0, Config.BrickWarmupPerTick) / Candidates.Length + (index < math.max(0, Config.BrickWarmupPerTick) % Candidates.Length ? 1 : 0);
            foreach (var touched in Touched)
                if (touched.Grid == grid && Summaries.HasComponent(touched.Brick) && Bricks[touched.Brick].SolidCount > 0) Check(touched.Brick);
            foreach (var slot in Reader.Slots[grid])
            {
                if (quota == 0) break;
                if (slot.Brick == Entity.Null || Bricks[slot.Brick].SolidCount == 0 || Summaries[slot.Brick].State != BrickConnectivity.Unknown) continue;
                Check(slot.Brick); quota--;
            }
            var runner = new Race { Job = this, Grid = grid, Definition = Reader.Grids[grid], Slots = Reader.Slots[grid], InstanceBuffer = Instances[grid],
                Nodes = new NativeList<Node>(Allocator.Temp), Sets = new NativeList<Set>(Allocator.Temp), Heap = new NativeList<HeapItem>(Allocator.Temp),
                CellNodes = new NativeHashMap<long, int>(64, Allocator.Temp), BrickNodes = new NativeHashMap<int, int>(16, Allocator.Temp) };
            runner.Job.Output.BeginForEachIndex(index);
            var result = runner.Run();
            runner.Job.Output.EndForEachIndex(); Results[index] = result;
            runner.Nodes.Dispose(); runner.Sets.Dispose(); runner.Heap.Dispose(); runner.CellNodes.Dispose(); runner.BrickNodes.Dispose();
        }
        void Check(Entity brick) => Summaries[brick] = BrickConnectivityCheck.Evaluate(Bricks[brick].SolidCount, Reader.Masks[brick], Reader.Cells[brick], Reader.Sparse[brick]);
        struct Node { public int Slot, Local, Set, Next; }
        struct Set { public int Parent, Weight, Head, Tail, Pending, Version; public bool Anchored; }
        struct HeapItem { public int Set, Weight, Version; }
        struct Bucket : System.IComparable<Bucket>
        {
            public int Set, Slot; public CellMask Mask;
            public int CompareTo(Bucket b) { int c = Set.CompareTo(b.Set); return c == 0 ? Slot.CompareTo(b.Slot) : c; }
        }
        struct Race
        {
            public IntegrityRaceJob Job;
            public Entity Grid;
            public VoxelGrid Definition;
            public DynamicBuffer<BrickSlot> Slots;
            public DynamicBuffer<BlockInstance> InstanceBuffer;
            public NativeList<Node> Nodes;
            public NativeList<Set> Sets;
            public NativeList<HeapItem> Heap;
            public NativeHashMap<long, int> CellNodes;
            public NativeHashMap<int, int> BrickNodes;
            int active, anchored;
            public IntegrityResult Run()
            {
                var seeds = Job.Seeds[Grid]; var state = Job.States[Grid];
                bool forced = state.ConsecutiveDeferrals >= Job.Config.IntegrityForceAfterDeferrals;
                // A prefix of seeds cannot prove the anchor when other components have no representative.
                // Keep the complete request and force all seeds after the same bounded deferral policy.
                if (!forced && seeds.Length > Job.Config.MaxSeedsPerGrid) return Defer(0);
                foreach (var seed in seeds)
                {
                    if (!Resolve(seed.Cell, out int slot, out int local) || FindNode(slot, local, out _)) continue;
                    int set = Sets.Length; Sets.Add(new Set { Parent = set, Head = -1, Tail = -1 });
                    AddNode(slot, local, set); Push(set);
                }
                int visits = 0, anchor = -1;
                while (active > 0)
                {
                    int set = Pop();
                    if (active == 1 && anchored == 0)
                    {
                        // The survivor is only a lower bound until exhausted. Equal-size components
                        // must still honour the canonical lowest-set-id anchor tie-break.
                        bool proven = true;
                        for (int i = 0; i < Sets.Length; i++)
                            if (i != set && Sets[i].Parent == i && (Sets[i].Weight > Sets[set].Weight || (Sets[i].Weight == Sets[set].Weight && i < set))) { proven = false; break; }
                        if (proven) { anchor = set; break; }
                    }
                    if (!forced && visits >= Job.Config.IntegrityNodeBudgetPerGrid) return Defer(visits);
                    var s = Sets[set]; int nodeIndex = s.Head; var node = Nodes[nodeIndex];
                    SetActive(set, -1); s.Head = node.Next; s.Pending--; s.Version++; if (s.Pending == 0) s.Tail = -1; Sets[set] = s; SetActive(set, 1);
                    visits++; Expand(node); Push(Root(set));
                }
                if (anchor < 0)
                    for (int i = 0; i < Sets.Length; i++)
                    {
                        var s = Sets[i]; if (s.Parent != i) continue;
                        if (s.Anchored) { anchor = i; break; }
                        if (anchor < 0 || s.Weight > Sets[anchor].Weight) anchor = i;
                    }
                int islands = Emit(anchor);
                seeds.Clear(); state.ConsecutiveDeferrals = 0; Job.States[Grid] = state;
                return new IntegrityResult { Nodes = visits, Islands = islands };
            }
            IntegrityResult Defer(int visits)
            {
                var state = Job.States[Grid]; state.ConsecutiveDeferrals = (byte)math.min(255, state.ConsecutiveDeferrals + 1); Job.States[Grid] = state;
                return new IntegrityResult { Nodes = visits, Deferred = 1 };
            }
            int Root(int set)
            {
                while (Sets[set].Parent != set)
                {
                    var s = Sets[set]; s.Parent = Sets[s.Parent].Parent; Sets[set] = s; set = s.Parent;
                }
                return set;
            }
            void SetActive(int id, int delta)
            { var s = Sets[id]; if (s.Anchored) anchored += delta; else if (s.Pending > 0) active += delta; }
            bool Resolve(int3 p, out int slot, out int local)
            {
                slot = BrickMath.SlotIndex(BrickMath.BrickOf(p), Definition.BrickMin, Definition.BrickDims); local = -1;
                if (slot < 0 || slot >= Slots.Length) return false;
                var brick = Slots[slot].Brick; if (brick == Entity.Null) return false;
                int cell = BrickMath.LocalIndex(BrickMath.LocalOf(p));
                if ((Job.Reader.Masks[brick][cell >> 6].Bits & (1UL << (cell & 63))) == 0) return false;
                var summary = Job.Summaries[brick];
                if (summary.State != BrickConnectivity.Connected || summary.SolidCountAtCheck != Job.Bricks[brick].SolidCount) local = cell;
                return true;
            }
            bool FindNode(int slot, int local, out int node) => local < 0 ? BrickNodes.TryGetValue(slot, out node) : CellNodes.TryGetValue(((long)slot << 12) | (uint)local, out node);
            void Visit(int3 p, int set)
            {
                set = Root(set); if (!Resolve(p, out int slot, out int local)) return;
                if (FindNode(slot, local, out int index)) Union(set, Root(Nodes[index].Set));
                else AddNode(slot, local, set);
            }
            void AddNode(int slot, int local, int set)
            {
                var brick = Slots[slot].Brick; var s = Sets[set]; SetActive(set, -1);
                int node = Nodes.Length; Nodes.Add(new Node { Slot = slot, Local = local, Set = set, Next = -1 });
                if (local < 0) BrickNodes.Add(slot, node); else CellNodes.Add(((long)slot << 12) | (uint)local, node);
                if (s.Tail >= 0) { var tail = Nodes[s.Tail]; tail.Next = node; Nodes[s.Tail] = tail; } else s.Head = node;
                s.Tail = node; s.Pending++; s.Weight += local < 0 ? Job.Bricks[brick].SolidCount : 1;
                if (Definition.AnchorInstance >= 0)
                {
                    if (local >= 0) s.Anchored |= CellAt(brick, local).Ref == (Cell.InstanceBit | Definition.AnchorInstance);
                    else
                    {
                        var instance = InstanceBuffer[Definition.AnchorInstance]; ref var def = ref Job.Blocks.Value.Blocks[instance.Block];
                        for (int i = 0; i < def.Cells.Length; i++)
                        {
                            int3 p = instance.Origin + Job.Orientations.Value.Rotate(def.Cells[i], instance.Orient);
                            if (math.all(BrickMath.BrickOf(p) == Job.Bricks[brick].Coord) && Job.Reader.TryGetCell(Grid, p, out var cell) && cell.Ref == (Cell.InstanceBit | Definition.AnchorInstance)) { s.Anchored = true; break; }
                        }
                    }
                }
                s.Version++; Sets[set] = s; SetActive(set, 1);
            }
            void Union(int a, int b)
            {
                if (a == b) return; if (a > b) { int t = a; a = b; b = t; }
                var x = Sets[a]; var y = Sets[b]; SetActive(a, -1); SetActive(b, -1);
                // Append the smaller FIFO to the larger in O(1), independently of the canonical root id.
                if (x.Pending < y.Pending) { int h = x.Head, t = x.Tail; x.Head = y.Head; x.Tail = y.Tail; y.Head = h; y.Tail = t; }
                if (y.Head >= 0)
                {
                    if (x.Tail >= 0) { var tail = Nodes[x.Tail]; tail.Next = y.Head; Nodes[x.Tail] = tail; } else x.Head = y.Head;
                    x.Tail = y.Tail;
                }
                x.Pending += y.Pending; x.Weight += y.Weight; x.Anchored |= y.Anchored; x.Version++;
                y.Parent = a; Sets[a] = x; Sets[b] = y; SetActive(a, 1);
            }
            Cell CellAt(Entity brick, int local) => BrickWrite.Get(Job.Reader.Cells[brick], Job.Reader.Sparse[brick], local);
            void Expand(Node node)
            {
                Entity brick = Slots[node.Slot].Brick; int3 coord = Job.Bricks[brick].Coord;
                if (node.Local >= 0)
                {
                    int3 p = BrickMath.CellOf(coord, node.Local);
                    for (int face = 0; face < 6; face++) Visit(p + BrickMath.FaceDirection((BlockFace)face), node.Set);
                    InstanceLinks(CellAt(brick, node.Local), node.Set); return;
                }
                // Facing layers are joined only where both masks have solid cells; Visit maps the neighbour to its node.
                var mask = Job.Reader.Masks[brick];
                for (int face = 0; face < 6; face++)
                {
                    int3 direction = BrickMath.FaceDirection((BlockFace)face);
                    for (int v = 0; v < 16; v++) for (int u = 0; u < 16; u++)
                    {
                        int3 local = direction.x != 0 ? new int3(direction.x > 0 ? 15 : 0, u, v) :
                            direction.y != 0 ? new int3(u, direction.y > 0 ? 15 : 0, v) : new int3(u, v, direction.z > 0 ? 15 : 0);
                        int index = BrickMath.LocalIndex(local);
                        if ((mask[index >> 6].Bits & (1UL << (index & 63))) != 0) Visit(coord * 16 + local + direction, node.Set);
                    }
                }
                if (Job.Summaries[brick].HasInstanceCells == 0) return;
                using var seen = new NativeHashSet<int>(16, Allocator.Temp);
                for (int word = 0; word < 64; word++)
                    for (ulong bits = mask[word].Bits; bits != 0; bits &= bits - 1)
                    {
                        var cell = CellAt(brick, word * 64 + math.tzcnt(bits));
                        if (cell.IsInstance && seen.Add(cell.InstanceIndex)) InstanceLinks(cell, node.Set);
                    }
            }
            void InstanceLinks(Cell cell, int set)
            {
                if (!cell.IsInstance) return;
                var instance = InstanceBuffer[cell.InstanceIndex]; ref var def = ref Job.Blocks.Value.Blocks[instance.Block];
                for (int i = 0; i < def.Cells.Length; i++)
                {
                    int3 p = instance.Origin + Job.Orientations.Value.Rotate(def.Cells[i], instance.Orient);
                    if (Job.Reader.TryGetCell(Grid, p, out var other) && other.Ref == cell.Ref) Visit(p, set);
                }
            }
            static bool Less(HeapItem a, HeapItem b) => a.Weight < b.Weight || (a.Weight == b.Weight && a.Set < b.Set);
            void Push(int id)
            {
                var s = Sets[id]; if (s.Anchored || s.Pending == 0) return;
                var item = new HeapItem { Set = id, Weight = s.Weight, Version = s.Version };
                int i = Heap.Length; Heap.Add(item);
                while (i > 0) { int parent = (i - 1) / 2; if (!Less(item, Heap[parent])) break; Heap[i] = Heap[parent]; i = parent; } Heap[i] = item;
            }
            int Pop()
            {
                while (Heap.Length > 0)
                {
                    var first = Heap[0]; var last = Heap[Heap.Length - 1]; Heap.RemoveAt(Heap.Length - 1);
                    if (Heap.Length > 0)
                    {
                        int i = 0;
                        while (i * 2 + 1 < Heap.Length)
                        {
                            int child = i * 2 + 1; if (child + 1 < Heap.Length && Less(Heap[child + 1], Heap[child])) child++;
                            if (!Less(Heap[child], last)) break; Heap[i] = Heap[child]; i = child;
                        }
                        Heap[i] = last;
                    }
                    var s = Sets[first.Set]; if (s.Parent == first.Set && s.Pending > 0 && !s.Anchored && s.Version == first.Version) return first.Set;
                }
                return -1;
            }
            int Emit(int anchor)
            {
                using var lookup = new NativeHashMap<int2, int>(16, Allocator.Temp);
                using var buckets = new NativeList<Bucket>(Allocator.Temp);
                foreach (var node in Nodes)
                {
                    int set = Root(node.Set); if (set == anchor || Sets[set].Pending != 0) continue;
                    var key = new int2(set, node.Slot);
                    if (!lookup.TryGetValue(key, out int index)) { index = buckets.Length; lookup.Add(key, index); buckets.Add(new Bucket { Set = set, Slot = node.Slot }); }
                    ref var bucket = ref buckets.ElementAt(index);
                    if (node.Local >= 0) bucket.Mask.Words[node.Local >> 6] |= 1UL << (node.Local & 63);
                    else { var mask = Job.Reader.Masks[Slots[node.Slot].Brick]; for (int w = 0; w < 64; w++) bucket.Mask.Words[w] |= mask[w].Bits; }
                }
                buckets.Sort(); int islands = 0;
                for (int first = 0; first < buckets.Length;)
                {
                    int end = first + 1; while (end < buckets.Length && buckets[end].Set == buckets[first].Set) end++;
                    var island = new IslandRecord { Grid = Grid, Ordinal = buckets[first].Set, MaskCount = end - first, CellMin = new int3(int.MaxValue), CellMax = new int3(int.MinValue) };
                    for (int i = first; i < end; i++)
                    {
                        var bucket = buckets[i]; var brick = Slots[bucket.Slot].Brick; var coord = Job.Bricks[brick].Coord;
                        for (int w = 0; w < 64; w++) for (ulong bits = bucket.Mask.Words[w]; bits != 0; bits &= bits - 1)
                        {
                            int local = w * 64 + math.tzcnt(bits); int3 p = BrickMath.CellOf(coord, local); island.CellCount++;
                            island.CellMin = math.min(island.CellMin, p); island.CellMax = math.max(island.CellMax, p);
                            var cell = CellAt(brick, local); if (cell.IsInstance && Job.Blocks.Value.Blocks[InstanceBuffer[cell.InstanceIndex].Block].Kind == BlockKind.Functional) island.HasFunctional = 1;
                        }
                    }
                    Job.Output.Write(island); for (int i = first; i < end; i++) { var b = buckets[i]; Job.Output.Write(new IslandMask { SlotIndex = b.Slot, Mask = b.Mask }); }
                    islands++; first = end;
                }
                return islands;
            }
        }
    }
}
