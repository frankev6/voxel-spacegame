using System;
using System.Collections.Generic;

namespace Game.Galaxy.Data
{
    /// <summary>Session-owned galaxy state; collections have deterministic save order (SPEC-30 §4.6).</summary>
    public sealed class GalaxyState : IDisposable
    {
        readonly List<uint> known = new(), visited = new();
        readonly SortedDictionary<uint, StarSystemDelta> deltas = new();
        StarSystemId routeDestination;
        public GalaxyState() { Known = known.AsReadOnly(); Visited = visited.AsReadOnly(); }
        public ulong Seed;
        public int GeneratorVersion;
        public StarSystemId CurrentStarSystem;
        public bool HasPlayerLocation;
        public PlayerLocation PlayerLocation;
        public StarSystemId RouteDestination
        {
            get => routeDestination;
            set { if (routeDestination.Equals(value)) return; routeDestination = value; Revision++; }
        }
        public uint Revision { get; private set; }
        public IReadOnlyList<uint> Known { get; }
        public IReadOnlyList<uint> Visited { get; }
        public bool AddKnown(StarSystemId id) => Insert(known, id.Value);
        public bool AddVisited(StarSystemId id) => Insert(visited, id.Value);
        bool Insert(List<uint> list, uint id)
        {
            int index = list.BinarySearch(id); if (index >= 0) return false;
            list.Insert(~index, id); Revision++; return true;
        }
        public bool IsKnown(StarSystemId id) => known.BinarySearch(id.Value) >= 0;
        public bool IsVisited(StarSystemId id) => visited.BinarySearch(id.Value) >= 0;
        public StarSystemDelta GetOrCreateDelta(StarSystemId id)
        {
            if (!deltas.TryGetValue(id.Value, out var delta))
            { delta = new StarSystemDelta(id); deltas.Add(id.Value, delta); }
            return delta;
        }
        public bool TryGetDelta(StarSystemId id, out StarSystemDelta delta) => deltas.TryGetValue(id.Value, out delta);
        public IEnumerable<StarSystemDelta> DeltasSorted()
        { foreach (var pair in deltas) if (!pair.Value.IsEmpty) yield return pair.Value; }
        public void Clear()
        {
            foreach (var pair in deltas) pair.Value.Dispose();
            deltas.Clear(); known.Clear(); visited.Clear();
            Seed = 0; GeneratorVersion = 0; CurrentStarSystem = default;
            HasPlayerLocation = false; PlayerLocation = default; routeDestination = default; Revision++;
        }
        public void Dispose() => Clear();
    }
}
