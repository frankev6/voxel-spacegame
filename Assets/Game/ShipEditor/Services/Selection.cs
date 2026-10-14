using System;
using System.Collections.Generic;
using Game.ShipEditor.Data;
using Game.ShipEditor.Validation;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §5.11: origin selection and atomic, damage-preserving transformations.</summary>
    internal sealed class Selection : IDisposable
    {
        const int Limit = 262144;
        readonly EditSession session;
        readonly EditorContext content;
        readonly SymmetryMapper mirrors;
        readonly EditBatch batch = new();
        readonly ApplyReport report = new();
        NativeHashSet<int3> origins = new(64, Allocator.Persistent);
        NativeList<BlockRecord> source = new(Allocator.Persistent), target = new(Allocator.Persistent);
        NativeList<byte> damage = new(Allocator.Persistent);
        internal int Count => origins.Count;
        internal int Version { get; private set; }
        internal PlacementError Error { get; private set; }
        internal Selection(EditSession session, EditorContext content)
        { this.session = session; this.content = content; mirrors = new SymmetryMapper(content); }
        internal bool Contains(int3 origin) => origins.Contains(origin);
        internal bool Add(int3 origin)
        {
            if (origins.Contains(origin)) return false;
            if (origins.Count >= Limit) { Error = PlacementError.OverBudget; return false; }
            origins.Add(origin); Version++; return true;
        }
        internal void Toggle(int3 origin)
        { if (origins.Remove(origin)) Version++; else Add(origin); }
        internal void Clear() { if (origins.Count != 0) { origins.Clear(); Version++; } }
        internal void SelectAll()
        {
            using var all = new NativeHashSet<int3>(64, Allocator.Temp);
            session.Placement.AllOrigins(all);
            if (all.Count > Limit) { Error = PlacementError.OverBudget; return; }
            origins.Clear(); foreach (var origin in all) origins.Add(origin); Version++;
        }
        internal void CopyRecords(NativeList<BlockRecord> records)
        {
            records.Clear();
            foreach (var origin in origins) session.Placement.AtOrigin(origin, records);
            records.AsArray().Sort(new CanonicalOrder());
        }
        internal void Box(int3 min, int3 max, bool additive)
        {
            long volume = ((long)max.x - min.x + 1) * ((long)max.y - min.y + 1) * ((long)max.z - min.z + 1);
            if (volume < 0 || volume > content.Config.MaxBatchCells) { Error = PlacementError.OverBudget; return; }
            using var found = new NativeHashSet<int3>(64, Allocator.Temp);
            if (additive) foreach (var origin in origins) found.Add(origin);
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    for (int z = min.z; z <= max.z; z++)
                    {
                        var origin = new int3(x, y, z);
                        if (!session.Placement.HasOrigin(origin)) continue;
                        found.Add(origin);
                        if (found.Count > Limit) { Error = PlacementError.OverBudget; return; }
                    }
            origins.Clear(); foreach (var origin in found) origins.Add(origin); Error = PlacementError.None; Version++;
        }
        internal void Capture(NativeList<BlockRecord> records, NativeList<byte> bytes)
        {
            CopyRecords(records); bytes.Clear();
            var reader = new DraftReader(session.Draft, content);
            for (int i = 0; i < records.Length; i++)
            { var record = records[i]; record.DamageOffset = reader.CaptureDamage(record, bytes); records[i] = record; }
        }
        internal bool Move(int3 delta, int stroke)
        {
            Prepare(); if (source.Length == 0) return false;
            try
            {
                foreach (var original in source)
                {
                    var record = original;
                    for (int axis = 0; axis < 3; axis++) record.Origin[axis] = checked(record.Origin[axis] + delta[axis]);
                    target.Add(record);
                }
            }
            catch (OverflowException) { Error = PlacementError.OutOfBounds; return false; }
            batch.Damage.AddRange(damage.AsArray());
            return Apply(stroke, "Move selection");
        }
        internal bool Rotate(int quarterTurns, int stroke)
        {
            Prepare(); if (source.Length == 0) return false;
            byte rotation = content.Orientations.Value.AxisTurn(1, quarterTurns);
            try
            {
                Bounds(source, out var min, out var max);
                foreach (var original in source)
                {
                    var record = original;
                    record.Origin = RotateChecked(original.Origin, rotation);
                    record.Orient = content.Orientations.Value.Compose(rotation, original.Orient);
                    // Rotation keeps canonical local-cell indices, so the original damage bytes remain canonical.
                    target.Add(record);
                }
                Bounds(target, out var rotatedMin, out var rotatedMax);
                int3 shift = default;
                for (int axis = 0; axis < 3; axis++)
                {
                    long oldSize = (long)max[axis] - min[axis], newSize = (long)rotatedMax[axis] - rotatedMin[axis];
                    // Mixed odd/even spans cannot share a lattice centre after a quarter turn.
                    // Truncate the half-cell difference toward zero so inverse/four turns restore the exact origin.
                    shift[axis] = checked((int)((long)min[axis] + (oldSize - newSize) / 2 - rotatedMin[axis]));
                }
                for (int i = 0; i < target.Length; i++)
                {
                    var record = target[i];
                    for (int axis = 0; axis < 3; axis++) record.Origin[axis] = checked(record.Origin[axis] + shift[axis]);
                    target[i] = record;
                }
            }
            catch (OverflowException) { Error = PlacementError.OutOfBounds; return false; }
            batch.Damage.AddRange(damage.AsArray()); return Apply(stroke, "Rotate selection");
        }
        internal bool Mirror(int stroke)
        {
            Prepare(); if (source.Length == 0) return false;
            Bounds(source, out var min, out var max);
            long plane = (long)min.x + max.x + 1;
            if (plane < int.MinValue || plane > int.MaxValue) { Error = PlacementError.OutOfBounds; return false; }
            foreach (var record in source)
            {
                Error = mirrors.Mirror(record, damage.AsArray(), 0, (int)plane, batch.Damage, out var mirrored);
                if (Error != PlacementError.None) return false;
                target.Add(mirrored);
            }
            return Apply(stroke, "Mirror selection");
        }
        internal bool Delete(int stroke, string label = "Delete selection")
        { Prepare(); return source.Length != 0 && Apply(stroke, label); }
        void Prepare()
        { Error = PlacementError.None; batch.Clear(); target.Clear(); Capture(source, damage); }
        bool Apply(int stroke, string label)
        {
            foreach (var original in source)
            { var record = original; record.DamageOffset = -1; batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = record }); }
            foreach (var record in target) batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = record });
            batch.Groups.Add(new BatchGroup { OpStart = 0, OpCount = batch.Ops.Length });
            bool applied = session.TryApply(batch, AtomicityMode.AllOrNothing, stroke, ToolId.Select, label, report);
            Error = report.GroupErrors.Count > 0 ? report.GroupErrors[0] : PlacementError.None;
            if (applied) { origins.Clear(); foreach (var record in target) origins.Add(record.Origin); Version++; }
            return applied;
        }
        internal void Bounds(NativeList<BlockRecord> records, out int3 min, out int3 max)
        {
            min = new int3(int.MaxValue); max = new int3(int.MinValue);
            foreach (var record in records)
            {
                var footprint = content.Footprints.Get(record.Block, record.Orient);
                int3 low = default, high = default;
                for (int axis = 0; axis < 3; axis++)
                { low[axis] = checked(record.Origin[axis] + footprint.Min[axis]); high[axis] = checked(record.Origin[axis] + footprint.Max[axis]); }
                min = math.min(min, low); max = math.max(max, high);
            }
        }
        int3 RotateChecked(int3 value, byte orientation)
        {
            var matrix = content.Orientations.Value.Matrices[orientation]; int3 result = default;
            for (int axis = 0; axis < 3; axis++)
                result[axis] = checked((int)((long)matrix.c0[axis] * value.x + (long)matrix.c1[axis] * value.y + (long)matrix.c2[axis] * value.z));
            return result;
        }
        readonly struct CanonicalOrder : IComparer<BlockRecord>
        {
            public int Compare(BlockRecord a, BlockRecord b)
            {
                int order = a.Origin.x.CompareTo(b.Origin.x); if (order != 0) return order;
                order = a.Origin.y.CompareTo(b.Origin.y); if (order != 0) return order;
                order = a.Origin.z.CompareTo(b.Origin.z); if (order != 0) return order;
                order = a.Block.CompareTo(b.Block); if (order != 0) return order;
                order = a.Orient.CompareTo(b.Orient); return order != 0 ? order : a.Color.CompareTo(b.Color);
            }
        }
        public void Dispose()
        {
            batch.Dispose(); if (origins.IsCreated) origins.Dispose(); if (source.IsCreated) source.Dispose();
            if (target.IsCreated) target.Dispose(); if (damage.IsCreated) damage.Dispose();
        }
    }
}
