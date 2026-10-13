using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>The generated galaxy. Owns native memory (Allocator.Persistent); disposed by GalaxyService. Implements SPEC-30 §4.</summary>
    public sealed class GalaxyDef : System.IDisposable
    {
        public ulong Seed { get; }
        public int GeneratorVersion { get; }
        public ulong Hash { get; }                                       // GalaxyHash.Compute, §5.4
        public Unity.Collections.NativeArray<StarSystemDef> StarSystems; // sorted by Id.Value ascending
        public Unity.Collections.NativeArray<LaneDef> Lanes;             // sorted by (A.Value, B.Value)
        public Unity.Collections.NativeArray<int> Adjacency;             // lane indices; per star system sorted by neighbour Id.Value
        public Unity.Collections.NativeHashMap<uint, int> IndexById;     // StarSystemId.Value → index in StarSystems
        public Unity.Collections.NativeArray<BodyTemplateParams> BodyTemplateParams; // index = body template index
        public Unity.Collections.NativeArray<AuthoredBodyParams> AuthoredBodies;     // all authored bodies, grouped
        public Unity.Collections.NativeArray<int2> AuthoredBodyRanges;   // per authored star system: (start, count) into AuthoredBodies
        public Unity.Collections.NativeArray<StarSystemTemplateParams> TemplateParams; // index = TemplateIndex; authored systems use AuthoredTemplateParams
        public Unity.Collections.NativeArray<StarSystemTemplateParams> AuthoredTemplateParams; // index = AuthoredIndex - 1
        public System.Collections.Generic.IReadOnlyList<StarSystemTemplateDefinition> Templates { get; }   // templatePool order
        public System.Collections.Generic.IReadOnlyList<AuthoredStarSystemDefinition> Authored { get; }     // sorted by ID (ordinal)
        public System.Collections.Generic.IReadOnlyList<LaneGateSpec> LaneGates { get; }
        public System.Collections.Generic.IReadOnlyList<BodyTemplateDefinition> BodyTemplates { get; }      // catalog.All<BodyTemplateDefinition>() order
        public StarSystemId StartStarSystem { get; }

        public Unity.Collections.NativeArray<WeightedIndex> PoolEntries;

        public bool TryGetIndex(StarSystemId id, out int index)
        {
            index = -1;
            return IndexById.IsCreated && IndexById.TryGetValue(id.Value, out index);
        }

        public unsafe ref readonly StarSystemDef Get(StarSystemId id)
        {
            if (!TryGetIndex(id, out int index)) throw new System.Collections.Generic.KeyNotFoundException(id.ToString());
            return ref Unity.Collections.LowLevel.Unsafe.UnsafeUtility.ArrayElementAsRef<StarSystemDef>(
                Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(StarSystems), index);
        }

        public int LaneCountOf(StarSystemId id) => Get(id).LaneCount;

        public int LaneIndexOf(StarSystemId id, int adjacencySlot)
        {
            ref readonly var system = ref Get(id);
            if ((uint)adjacencySlot >= system.LaneCount) throw new System.ArgumentOutOfRangeException(nameof(adjacencySlot));
            return Adjacency[system.FirstAdjacency + adjacencySlot];
        }

        public static StarSystemId Neighbour(in LaneDef lane, StarSystemId self)
        {
            if (lane.A.Equals(self)) return lane.B;
            if (lane.B.Equals(self)) return lane.A;
            throw new System.ArgumentException("Star system is not an endpoint of this lane.", nameof(self));
        }

        public bool TryFindLane(StarSystemId a, StarSystemId b, out int laneIndex)
        {
            laneIndex = -1;
            if (!Lanes.IsCreated) return false;
            if (a.Value > b.Value) (a, b) = (b, a);
            int low = 0, high = Lanes.Length - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                var lane = Lanes[middle];
                int comparison = lane.A.Value.CompareTo(a.Value);
                if (comparison == 0) comparison = lane.B.Value.CompareTo(b.Value);
                if (comparison == 0) { laneIndex = middle; return true; }
                if (comparison < 0) low = middle + 1;
                else high = middle - 1;
            }
            return false;
        }

        public ushort BodyTemplateIndexOf(BodyTemplateDefinition def)
        {
            if (def == null || BodyTemplates == null) return GalaxyConstants.NoIndex;
            for (int i = 0; i < BodyTemplates.Count; i++)
                if (BodyTemplates[i] == def) return checked((ushort)i);
            return GalaxyConstants.NoIndex;
        }

        public void Dispose()
        {
            if (StarSystems.IsCreated) StarSystems.Dispose();
            if (Lanes.IsCreated) Lanes.Dispose();
            if (Adjacency.IsCreated) Adjacency.Dispose();
            if (IndexById.IsCreated) IndexById.Dispose();
            if (BodyTemplateParams.IsCreated) BodyTemplateParams.Dispose();
            if (AuthoredBodies.IsCreated) AuthoredBodies.Dispose();
            if (AuthoredBodyRanges.IsCreated) AuthoredBodyRanges.Dispose();
            if (TemplateParams.IsCreated) TemplateParams.Dispose();
            if (AuthoredTemplateParams.IsCreated) AuthoredTemplateParams.Dispose();
            if (PoolEntries.IsCreated) PoolEntries.Dispose();
        }
        // TODO(GAL-004): builder initializes owned arrays and managed definition lists.
    }
}
