using System;
using System.Collections.Generic;
using Game.ShipEditor.Data;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Services
{
    /// <summary>Controller-owned pristine records, canonical and relative to the full footprint AABB minimum.</summary>
    internal sealed class Clipboard : IDisposable
    {
        readonly EditorContext content;
        NativeList<BlockRecord> records = new(Allocator.Persistent), captured = new(Allocator.Persistent);
        internal NativeArray<BlockRecord>.ReadOnly Records => records.AsArray().AsReadOnly();
        internal int Count => records.Length;
        internal PlacementError Error { get; private set; }
        internal Clipboard(EditorContext content) => this.content = content;
        internal bool Copy(Selection selection) { selection.CopyRecords(captured); return Store(); }
        internal bool Copy(VoxelStructure source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            new DraftReader(source, content).Enumerate(captured); return Store();
        }
        bool Store()
        {
            Error = PlacementError.None;
            if (captured.Length == 0) return false; // An empty copy keeps the previous clipboard.
            try
            {
                int3 min = new(int.MaxValue);
                foreach (var record in captured)
                {
                    var footprint = content.Footprints.Get(record.Block, record.Orient); int3 low = default;
                    for (int axis = 0; axis < 3; axis++) low[axis] = checked(record.Origin[axis] + footprint.Min[axis]);
                    min = math.min(min, low);
                }
                for (int i = 0; i < captured.Length; i++)
                {
                    var record = captured[i];
                    for (int axis = 0; axis < 3; axis++) record.Origin[axis] = checked(record.Origin[axis] - min[axis]);
                    record.DamageOffset = -1; captured[i] = record;
                }
            }
            catch (OverflowException) { Error = PlacementError.OutOfBounds; return false; }
            captured.AsArray().Sort(new CanonicalOrder());
            var previous = records; records = captured; captured = previous;
            return true;
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
        { if (records.IsCreated) records.Dispose(); if (captured.IsCreated) captured.Dispose(); }
    }
}
