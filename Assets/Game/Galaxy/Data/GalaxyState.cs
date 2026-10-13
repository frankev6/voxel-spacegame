using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Everything the galaxy saves. Held by GameSession (SPEC-01); restored in place. Implements SPEC-30 §4.</summary>
    public sealed class GalaxyState : System.IDisposable
    {
        public ulong Seed;
        public int GeneratorVersion;
        public StarSystemId CurrentStarSystem;
        public bool HasPlayerLocation;
        public PlayerLocation PlayerLocation;
        public StarSystemId RouteDestination
        {
            get => throw new System.NotImplementedException("GAL-007");
            set => throw new System.NotImplementedException("GAL-007");
        }    // written by SPEC-32; the setter increments Revision when the value changes
        public uint Revision { get; }                         // +1 on every AddKnown/AddVisited that returns true, on a RouteDestination change
                                                              // and on Clear (never reset; consumers compare stamps)
        public System.Collections.Generic.IReadOnlyList<uint> Known { get; }    // sorted ascending
        public System.Collections.Generic.IReadOnlyList<uint> Visited { get; }  // sorted ascending
        public bool AddKnown(StarSystemId id) => throw new System.NotImplementedException("GAL-007"); public bool AddVisited(StarSystemId id) => throw new System.NotImplementedException("GAL-007");
        public bool IsKnown(StarSystemId id) => throw new System.NotImplementedException("GAL-007"); public bool IsVisited(StarSystemId id) => throw new System.NotImplementedException("GAL-007");
        public StarSystemDelta GetOrCreateDelta(StarSystemId id) => throw new System.NotImplementedException("GAL-007");
        public bool TryGetDelta(StarSystemId id, out StarSystemDelta d) => throw new System.NotImplementedException("GAL-007");
        public System.Collections.Generic.IEnumerable<StarSystemDelta> DeltasSorted() => throw new System.NotImplementedException("GAL-007");   // ascending StarSystemId, skips IsEmpty
        public void Clear() => throw new System.NotImplementedException("GAL-007");                                  // disposes deltas, resets every field to default
        public void Dispose() => throw new System.NotImplementedException("GAL-007");
        // TODO(GAL-007): implement the behavior specified by SPEC-30.
    }
}
