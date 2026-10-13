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

        public bool TryGetIndex(StarSystemId id, out int index) => throw new System.NotImplementedException("GAL-004");
        public ref readonly StarSystemDef Get(StarSystemId id) => throw new System.NotImplementedException("GAL-004");          // KeyNotFoundException if absent
        public int LaneCountOf(StarSystemId id) => throw new System.NotImplementedException("GAL-004");
        public int LaneIndexOf(StarSystemId id, int adjacencySlot) => throw new System.NotImplementedException("GAL-004");      // 0 ≤ adjacencySlot < LaneCount
        public static StarSystemId Neighbour(in LaneDef lane, StarSystemId self) => throw new System.NotImplementedException("GAL-004"); // the other end
        public bool TryFindLane(StarSystemId a, StarSystemId b, out int laneIndex) => throw new System.NotImplementedException("GAL-004");
        public ushort BodyTemplateIndexOf(BodyTemplateDefinition def) => throw new System.NotImplementedException("GAL-004");  // NoIndex if not in BodyTemplates
        public void Dispose() => throw new System.NotImplementedException("GAL-004");                                           // safe to call twice
        // TODO(GAL-004): implement the behavior specified by SPEC-30.
    }
}
