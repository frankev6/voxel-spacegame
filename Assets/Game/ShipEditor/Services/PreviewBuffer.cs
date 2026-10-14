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
        internal bool HasBounds { get; private set; }
        internal Unity.Mathematics.int3 BoundsMin { get; private set; }
        internal Unity.Mathematics.int3 BoundsMax { get; private set; }
        internal string StatusText { get; private set; } = "";
        internal void SetBounds(Unity.Mathematics.int3 min, Unity.Mathematics.int3 max)
        { Clear(); HasBounds = true; BoundsMin = min; BoundsMax = max; }
        internal PlacementError Error { get; private set; }
        internal int Version { get; private set; }
        internal void SetMessage(string text, PlacementError error = PlacementError.None)
        { StatusText = text ?? ""; Error = error; Version++; }
        internal void Clear() { records.Clear(); HasBounds = false; Error = PlacementError.None; StatusText = ""; Version++; }
        internal void Set(NativeArray<BlockRecord> value, PlacementError error)
        { records.Clear(); HasBounds = false; records.AddRange(value); Error = error; StatusText = ""; Version++; }
        public void Dispose() { if (records.IsCreated) records.Dispose(); records = default; }
    }
}
