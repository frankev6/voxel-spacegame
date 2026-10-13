using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Saved changes to one generated star system. Implements SPEC-30 §4.6.</summary>
    public sealed class StarSystemDelta : System.IDisposable
    {
        public StarSystemDelta(StarSystemId id) => throw new System.NotImplementedException("GAL-007");
        public StarSystemId StarSystem { get; }
        public uint VisitCount { get; }
        public System.Collections.Generic.IReadOnlyList<ulong> Destroyed { get; }          // sorted ascending, unique
        public System.Collections.Generic.IReadOnlyList<ulong> Interacted { get; }         // sorted ascending, unique
        public System.Collections.Generic.IReadOnlyList<StorySpawnRecord> StorySpawns { get; } // append-only; index = slot
        public System.Collections.Generic.IReadOnlyList<ReplacementRecord> Replacements { get; } // sorted by BodyKey, unique
        public System.Collections.Generic.IReadOnlyList<StructureDiffRecord> StructureDiffs { get; } // sorted by BodyKey, unique

        public void IncrementVisit() => throw new System.NotImplementedException("GAL-007");
        public bool MarkDestroyed(ulong bodyKey) => throw new System.NotImplementedException("GAL-007");              // false if already present
        public bool MarkInteracted(ulong bodyKey) => throw new System.NotImplementedException("GAL-007");
        public int AddStorySpawn(in StorySpawnRecord r) => throw new System.NotImplementedException("GAL-007");       // returns slot; throws when Count == 65535
        public bool RemoveStorySpawn(int slot) => throw new System.NotImplementedException("GAL-007");                // sets Removed; false if already removed or out of range
        public void SetReplacement(ulong bodyKey, Game.Core.ContentId template) => throw new System.NotImplementedException("GAL-007");   // insert or overwrite
        public void SetStructure(ulong bodyKey, uint revision, Game.Voxel.Model.VoxelStructure s) => throw new System.NotImplementedException("GAL-007"); // takes ownership; disposes the previous one
        public bool RemoveStructure(ulong bodyKey) => throw new System.NotImplementedException("GAL-007");            // disposes it
        public bool TryGetStructure(ulong bodyKey, out StructureDiffRecord r) => throw new System.NotImplementedException("GAL-007");
        public bool IsEmpty { get; }                           // VisitCount == 0 and all lists empty
        public void Dispose() => throw new System.NotImplementedException("GAL-007");                                 // disposes structures
        // TODO(GAL-007): implement the behavior specified by SPEC-30.
    }
}
