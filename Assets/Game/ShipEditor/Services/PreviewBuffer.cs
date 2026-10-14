using System;
using Game.ShipEditor.Data;
using Unity.Collections;

namespace Game.ShipEditor.Services
{
    /// <summary>Reusable controller-owned preview records. Rendering borrows them until the next rebuild.</summary>
    internal sealed class PreviewBuffer : IDisposable
    {
        NativeList<BlockRecord> records = new(Allocator.Persistent);
        internal NativeArray<BlockRecord> Records => records.AsArray();
        internal PlacementError Error { get; private set; }
        internal int Version { get; private set; }
        internal void Clear() { records.Clear(); Error = PlacementError.None; Version++; }
        internal void Set(NativeArray<BlockRecord> value, PlacementError error)
        { records.Clear(); records.AddRange(value); Error = error; Version++; }
        public void Dispose() { if (records.IsCreated) records.Dispose(); records = default; }
    }
}
