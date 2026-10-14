using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Validation
{
    /// <summary>SPEC-51 §4.3: overlapping empty-space reservations are reference counted.</summary>
    internal sealed class ClearanceIndex : IDisposable
    {
        NativeHashMap<int3, ushort> counts = new(64, Allocator.Persistent);
        internal int this[int3 cell] => counts.TryGetValue(cell, out ushort count) ? count : 0;
        internal void Add(int3 cell, int delta)
        {
            int value = this[cell] + delta;
            if (value < 0 || value > ushort.MaxValue) throw new InvalidOperationException("EDT: invalid clearance reservation count.");
            if (value == 0) counts.Remove(cell); else counts[cell] = (ushort)value;
        }
        internal void Clear() => counts.Clear();
        public void Dispose() { if (counts.IsCreated) counts.Dispose(); }
    }
}
