using System;
using Game.ShipEditor.Data;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Validation
{
    /// <summary>SPEC-51 §5.4: reusable native overlays and an exact per-group rollback journal.</summary>
    internal sealed class ValidationScratch : IDisposable
    {
        internal struct CellState
        {
            internal BlockRecord Block;
            internal int Group;
            internal bool Present, Reserved;
        }
        struct Change
        {
            internal int Kind, BeforeInt;
            internal int3 Cell;
            internal CellState Before;
            internal bool Existed;
        }
        internal struct Checkpoint
        {
            internal int Journal, Candidates, Cells, Cores;
            internal int3 Min, Max;
            internal bool Bounds;
        }
        internal NativeHashMap<int3, CellState> Cells = new(64, Allocator.Persistent);
        internal NativeHashMap<int3, BlockRecord> Origins = new(64, Allocator.Persistent);
        internal NativeHashMap<int3, int> ClearanceDelta = new(64, Allocator.Persistent);
        internal NativeList<int> AcceptedOps = new(Allocator.Persistent);
        internal NativeList<int3> Candidates = new(Allocator.Persistent), Stack = new(Allocator.Persistent);
        internal NativeHashSet<int3> GroupCells = new(64, Allocator.Persistent), Visited = new(64, Allocator.Persistent);
        NativeList<Change> journal = new(Allocator.Persistent);
        internal int CellDelta, CoreDelta;
        internal int3 Min, Max;
        internal bool HasBounds;
        internal void Clear()
        {
            Cells.Clear(); Origins.Clear(); ClearanceDelta.Clear(); AcceptedOps.Clear(); Candidates.Clear();
            Stack.Clear(); GroupCells.Clear(); Visited.Clear(); journal.Clear(); CellDelta = CoreDelta = 0; HasBounds = false;
        }
        internal Checkpoint Mark() => new() { Journal = journal.Length, Candidates = Candidates.Length,
            Cells = CellDelta, Cores = CoreDelta, Min = Min, Max = Max, Bounds = HasBounds };
        internal void SetCell(int3 cell, CellState value)
        {
            bool found = Cells.TryGetValue(cell, out var before);
            journal.Add(new Change { Kind = 0, Cell = cell, Before = before, Existed = found }); Cells[cell] = value;
        }
        internal void SetOrigin(int3 cell, BlockRecord value)
        {
            bool found = Origins.TryGetValue(cell, out var before);
            journal.Add(new Change { Kind = 1, Cell = cell, Before = new CellState { Block = before }, Existed = found }); Origins[cell] = value;
        }
        internal void ChangeClearance(int3 cell, int delta)
        {
            bool found = ClearanceDelta.TryGetValue(cell, out int before);
            journal.Add(new Change { Kind = 2, Cell = cell, BeforeInt = before, Existed = found }); ClearanceDelta[cell] = before + delta;
        }
        internal void Include(int3 cell)
        { Min = HasBounds ? math.min(Min, cell) : cell; Max = HasBounds ? math.max(Max, cell) : cell; HasBounds = true; }
        internal void Rollback(Checkpoint point)
        {
            for (int i = journal.Length - 1; i >= point.Journal; i--)
            {
                var change = journal[i];
                if (change.Kind == 0) { if (change.Existed) Cells[change.Cell] = change.Before; else Cells.Remove(change.Cell); }
                else if (change.Kind == 1) { if (change.Existed) Origins[change.Cell] = change.Before.Block; else Origins.Remove(change.Cell); }
                else { if (change.Existed) ClearanceDelta[change.Cell] = change.BeforeInt; else ClearanceDelta.Remove(change.Cell); }
            }
            journal.ResizeUninitialized(point.Journal); Candidates.ResizeUninitialized(point.Candidates);
            CellDelta = point.Cells; CoreDelta = point.Cores; Min = point.Min; Max = point.Max; HasBounds = point.Bounds;
        }
        public void Dispose()
        {
            if (Cells.IsCreated) Cells.Dispose(); if (Origins.IsCreated) Origins.Dispose();
            if (ClearanceDelta.IsCreated) ClearanceDelta.Dispose(); if (AcceptedOps.IsCreated) AcceptedOps.Dispose();
            if (Candidates.IsCreated) Candidates.Dispose(); if (Stack.IsCreated) Stack.Dispose();
            if (GroupCells.IsCreated) GroupCells.Dispose(); if (Visited.IsCreated) Visited.Dispose(); if (journal.IsCreated) journal.Dispose();
        }
    }
}
