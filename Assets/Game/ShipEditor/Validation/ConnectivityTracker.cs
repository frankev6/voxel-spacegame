using System;
using Game.ShipEditor.Jobs;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.ShipEditor.Validation
{
    /// <summary>SPEC-51 §5.5. Owned brick labels; edits join pending readers before touching storage.</summary>
    internal sealed class ConnectivityTracker : IDisposable
    {
        NativeHashMap<int3,int> slots = new(16, Allocator.Persistent);
        NativeList<int3> bricks = new(Allocator.Persistent);
        NativeList<int> labels = new(Allocator.Persistent), spare = new(Allocator.Persistent);
        NativeList<int> parents = new(Allocator.Persistent), sizes = new(Allocator.Persistent), stack = new(Allocator.Persistent);
        NativeList<int> affected = new(Allocator.Persistent), neighbours = new(Allocator.Persistent);
        NativeList<int> unresolved = new(Allocator.Persistent), visited = new(Allocator.Persistent);
        NativeHashSet<int> seen = new(16, Allocator.Persistent);
        NativeReference<bool> hit = new(Allocator.Persistent);
        readonly int budget;
        readonly ProfilerMarker removeMarker = new("ConnectivityTracker.Remove"), relabelMarker = new("ConnectivityTracker.Relabel");
        JobHandle pendingJob;
        bool disposed;
        int islands;
        internal bool ConnectivityPending { get; private set; }
        internal ConnectivityTracker(int splitBudget = 8192) { if (splitBudget < 1) { Dispose(); throw new ArgumentOutOfRangeException(nameof(splitBudget)); } budget = splitBudget; }
        internal int IslandCount { get { CompleteRelabel(); return islands; } }
        internal int RootAt(int3 cell) { CompleteRelabel(); int i = ConnectivityCells.Index(cell, slots); return i < 0 ? -1 : Find(labels[i]); }
        internal int SizeOf(int root) { CompleteRelabel(); return root < 0 ? 0 : sizes[Find(root)]; }
        int Find(int label)
        {
            while (label >= 0 && parents[label] != label) { parents[label] = parents[parents[label]]; label = parents[label]; }
            return label;
        }
        int Ensure(int3 cell)
        {
            int3 brick = BrickMath.BrickOf(cell);
            if (!slots.TryGetValue(brick, out int slot))
            {
                slot = bricks.Length; slots.Add(brick, slot); bricks.Add(brick);
                int start = labels.Length; labels.ResizeUninitialized(start + 4096);
                for (int i = start; i < labels.Length; i++) labels[i] = -1;
            }
            return slot * 4096 + BrickMath.LocalIndex(BrickMath.LocalOf(cell));
        }
        internal void Initialize(VoxelStructure source)
        {
            CompleteRelabel(); slots.Clear(); bricks.Clear(); labels.Clear(); parents.Clear(); sizes.Clear(); islands = 0;
            using var order = source.SortedBrickOrder(Allocator.Temp);
            var view = VoxelStructureReadView.From(source);
            foreach (int b in order)
            {
                if (view.Bricks[b].SolidCount == 0) continue;
                int slotStart = Ensure(BrickMath.CellOf(view.Bricks[b].Coord, 0)) & ~4095;
                for (int i = 0; i < 4096; i++) if (!view.Read(b, i).IsAir) labels[slotStart+i] = 0;
            }
            FullRelabel(); CompleteRelabel();
        }
        internal void OnAdd(NativeArray<int3> cells)
        {
            CompleteRelabel();
            // Cell-wise union also handles holes that disconnect a damaged block's surviving footprint.
            foreach (int3 cell in cells)
            {
                int at = Ensure(cell); if (labels[at] >= 0) throw new InvalidOperationException("EDT: connectivity addition overlaps solid cell.");
                int root = -1;
                for (int d = 0; d < 6; d++)
                {
                    int n = ConnectivityCells.Index(cell + ConnectivityCells.Direction(d), slots);
                    int other = n < 0 ? -1 : Find(labels[n]); if (other < 0 || other == root) continue;
                    if (root < 0) root = other;
                    else { int low = math.min(root, other), high = math.max(root, other); parents[high] = low; sizes[low] += sizes[high]; sizes[high] = 0; islands--; root = low; }
                }
                if (root < 0) { root = parents.Length; parents.Add(root); sizes.Add(0); islands++; }
                labels[at] = root; sizes[root]++;
            }
        }
        internal void OnRemove(NativeArray<int3> cells)
        {
            CompleteRelabel(); using var marker = removeMarker.Auto();
            affected.Clear(); neighbours.Clear(); seen.Clear();
            foreach (int3 cell in cells)
            {
                int at = ConnectivityCells.Index(cell, slots);
                if (at < 0 || labels[at] < 0) throw new InvalidOperationException("EDT: connectivity removal is air.");
                int root = Find(labels[at]); labels[at] = -1; sizes[root]--;
                if (seen.Add(root)) affected.Add(root);
            }
            seen.Clear();
            foreach (int3 cell in cells)
                for (int d = 0; d < 6; d++)
                {
                    int n = ConnectivityCells.Index(cell + ConnectivityCells.Direction(d), slots);
                    if (n >= 0 && labels[n] >= 0 && seen.Add(n)) neighbours.Add(n);
                }
            foreach (int root in affected)
            {
                if (sizes[root] == 0) { islands--; continue; }
                unresolved.Clear();
                foreach (int n in neighbours) if (Find(labels[n]) == root) unresolved.Add(n);
                if (unresolved.Length <= 1) continue;
                while (unresolved.Length > 0)
                {
                    new BoundedSplitJob { Slots = slots, Bricks = bricks.AsArray(), Labels = labels.AsArray(), Unresolved = unresolved, Visited = visited, Seen = seen, BudgetHit = hit, Budget = budget }.Run();
                    if (hit.Value) { FullRelabel(); return; }
                    if (unresolved.Length == 0) break; // The last component keeps the original root.
                    int next = parents.Length; parents.Add(next); sizes.Add(visited.Length); sizes[root] -= visited.Length; islands++;
                    foreach (int index in visited) labels[index] = next;
                }
            }
        }
        internal void FullRelabel()
        {
            CompleteRelabel(); using var marker = relabelMarker.Auto(); spare.ResizeUninitialized(labels.Length);
            pendingJob = new IslandRelabelJob { Slots = slots, Bricks = bricks.AsArray(), Input = labels.AsArray(), Output = spare.AsArray(), Parents = parents, Sizes = sizes, Stack = stack }.Schedule();
            ConnectivityPending = true;
        }
        internal void CompleteRelabel()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ConnectivityTracker));
            if (!ConnectivityPending) return;
            pendingJob.Complete(); (labels, spare) = (spare, labels); islands = parents.Length; ConnectivityPending = false;
        }
        internal void DisconnectedCells(int3 coreCell, NativeList<int3> output, int limit)
        {
            CompleteRelabel(); int root = RootAt(coreCell);
            if (root < 0) { int largest = 0; for (int i = 0; i < sizes.Length; i++) if (sizes[i] > largest) { root = i; largest = sizes[i]; } }
            new DisconnectedCellsJob { Bricks = bricks.AsArray(), Labels = labels.AsArray(), Parents = parents.AsArray(), Cells = output, Root = root, Limit = math.max(0, limit) }.Run();
        }
        public void Dispose()
        {
            if (disposed) return; CompleteRelabel();
            slots.Dispose(); bricks.Dispose(); labels.Dispose(); spare.Dispose(); parents.Dispose(); sizes.Dispose(); stack.Dispose(); affected.Dispose(); neighbours.Dispose(); unresolved.Dispose(); visited.Dispose(); seen.Dispose(); hit.Dispose(); disposed = true;
        }
    }
}
