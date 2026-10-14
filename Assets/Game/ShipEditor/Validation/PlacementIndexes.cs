using System;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Validation
{
    /// <summary>Incremental placement state. No query rescans the entire draft.</summary>
    internal sealed class PlacementIndexes : IDisposable
    {
        internal readonly ClearanceIndex Clearance = new();
        internal readonly AxisHistogram X = new(), Y = new(), Z = new();
        NativeHashSet<int3> reservedMissing = new(64, Allocator.Persistent);
        NativeHashMap<int3, int> missingCounts = new(64, Allocator.Persistent);
        NativeHashMap<BlockRecord, BlockRecord> origins = new(64, Allocator.Persistent);
        NativeParallelMultiHashMap<int3, BlockRecord> byOrigin = new(64, Allocator.Persistent);
        NativeHashSet<BlockRecord> cores = new(4, Allocator.Persistent);
        internal int Cores => cores.Count;
        internal BlockRecord? Core
        {
            get
            {
                BlockRecord? result = null;
                foreach (var record in cores)
                {
                    if (!result.HasValue) { result = record; continue; }
                    var old = result.Value;
                    if (record.Origin.x < old.Origin.x || record.Origin.x == old.Origin.x &&
                        (record.Origin.y < old.Origin.y || record.Origin.y == old.Origin.y &&
                        (record.Origin.z < old.Origin.z || record.Origin.z == old.Origin.z &&
                        (record.Block < old.Block || record.Block == old.Block &&
                        (record.Orient < old.Orient || record.Orient == old.Orient && record.Color < old.Color))))) result = record;
                }
                return result;
            }
        }
        internal int SolidCells => X.Total;
        internal bool IsReservedMissing(int3 cell) => reservedMissing.Contains(cell);
        internal int MissingCount(int3 cell) => missingCounts.TryGetValue(cell, out int count) ? count : 0;
        internal bool TryGetBlock(BlockRecord identity, out BlockRecord block) => origins.TryGetValue(identity, out block);
        internal void AtOrigin(int3 origin, NativeList<BlockRecord> output)
        {
            if (byOrigin.TryGetFirstValue(origin, out var record, out var iterator))
                do { output.Add(record); } while (byOrigin.TryGetNextValue(out record, ref iterator));
        }
        internal bool HasOrigin(int3 origin) => byOrigin.ContainsKey(origin);
        internal void AllOrigins(NativeHashSet<int3> output)
        { foreach (var entry in origins) output.Add(entry.Key.Origin); }
        internal bool TryGetBounds(out int3 min, out int3 max)
        {
            bool exists = X.TryGetBounds(out int x0, out int x1);
            Y.TryGetBounds(out int y0, out int y1); Z.TryGetBounds(out int z0, out int z1);
            min = new int3(x0, y0, z0); max = new int3(x1, y1, z1); return exists;
        }
        internal void Initialize(VoxelStructure source, EditorContext context)
        {
            Clearance.Clear(); reservedMissing.Clear(); missingCounts.Clear(); origins.Clear(); byOrigin.Clear(); X.Clear(); Y.Clear(); Z.Clear(); cores.Clear();
            var reader = new DraftReader(source, context);
            using var records = new NativeList<BlockRecord>(Allocator.TempJob);
            using var bytes = new NativeList<byte>(Allocator.Temp);
            reader.Enumerate(records);
            foreach (var block in records)
            {
                bytes.Clear(); var record = block; record.DamageOffset = reader.CaptureDamage(record, bytes);
                Change(record, bytes.AsArray(), context, 1);
            }
        }
        internal void Apply(DiffCommand command, bool forward, EditorContext context)
        {
            for (int i = 0; i < command.Ops.Length; i++)
            {
                var op = command.Ops[forward ? i : command.Ops.Length - 1 - i];
                Change(op.Record, command.Damage.AsArray(), context, (op.Kind == DiffOpKind.Add) == forward ? 1 : -1);
            }
        }
        void Change(BlockRecord record, NativeArray<byte> bytes, EditorContext context, int delta)
        {
            var footprint = context.Footprints.Get(record.Block, record.Orient);
            if (delta > 0) { var identity = record; identity.DamageOffset = -1; origins.Add(identity, identity); byOrigin.Add(identity.Origin, identity); }
            else
            {
                origins.Remove(record);
                if (byOrigin.TryGetFirstValue(record.Origin, out var found, out var iterator))
                    do { if (found.Equals(record)) { byOrigin.Remove(iterator); break; } }
                    while (byOrigin.TryGetNextValue(out found, ref iterator));
            }
            if (context.Definitions.Value.Blocks[record.Block].IsCore)
            { if (delta > 0) cores.Add(record); else cores.Remove(record); }
            foreach (var clearance in footprint.Clearance) Clearance.Add(record.Origin + clearance, delta);
            for (int i = 0; i < footprint.Cells.Length; i++)
            {
                int3 cell = record.Origin + footprint.Cells[i];
                bool missing = record.DamageOffset >= 0 && bytes[record.DamageOffset + i] == 0;
                if (missing)
                {
                    int count = MissingCount(cell) + delta;
                    if (count == 0) { missingCounts.Remove(cell); reservedMissing.Remove(cell); }
                    else { missingCounts[cell] = count; reservedMissing.Add(cell); }
                }
                else { X.Add(cell.x, delta); Y.Add(cell.y, delta); Z.Add(cell.z, delta); }
            }
        }
        public void Dispose()
        {
            Clearance.Dispose();
            if (reservedMissing.IsCreated) reservedMissing.Dispose();
            if (origins.IsCreated) origins.Dispose();
            if (byOrigin.IsCreated) byOrigin.Dispose();
            if (cores.IsCreated) cores.Dispose(); if (missingCounts.IsCreated) missingCounts.Dispose();
        }
    }
}
