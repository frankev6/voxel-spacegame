using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Bodies of one star system: generated (§5.5), then deltas applied (§5.6). Owns native memory. Implements SPEC-30 §4.</summary>
    public sealed class StarSystemContent : System.IDisposable
    {
        public StarSystemId Id { get; }
        public Unity.Collections.NativeList<BodySpec> Bodies;   // Allocator.Persistent, sorted by Id.LocalKey ascending
        public double LaneRingRadiusM { get; internal set; }
        public double ExtentM { get; internal set; }
        public ulong GeneratedHash { get; internal set; }        // StarSystemContentHash over Bodies before deltas
        internal StarSystemContent(StarSystemId id)
        {
            Id = id;
            Bodies = new Unity.Collections.NativeList<BodySpec>(Unity.Collections.Allocator.Persistent);
        }
        public bool TryFind(ulong localKey, out int index)
        {
            index = -1;
            if (!Bodies.IsCreated) return false;
            int low = 0, high = Bodies.Length - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                ulong key = Bodies[middle].Id.LocalKey;
                if (key == localKey) { index = middle; return true; }
                if (key < localKey) low = middle + 1;
                else high = middle - 1;
            }
            return false;
        }
        public void Dispose() { if (Bodies.IsCreated) Bodies.Dispose(); }
    }
}
