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
        public bool TryFind(ulong localKey, out int index) => throw new System.NotImplementedException("GAL-006");       // binary search
        public void Dispose() => throw new System.NotImplementedException("GAL-006");
        // TODO(GAL-006): implement the behavior specified by SPEC-30.
    }
}
