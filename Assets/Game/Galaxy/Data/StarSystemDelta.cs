using System;
using System.Collections.Generic;
using Game.Core;
using Game.Voxel.Model;

namespace Game.Galaxy.Data
{
    /// <summary>Sorted saved changes and append-only story slots for one star system (SPEC-30 §4.6).</summary>
    public sealed class StarSystemDelta : IDisposable
    {
        readonly List<ulong> destroyed = new(), interacted = new();
        readonly List<StorySpawnRecord> stories = new();
        readonly List<ReplacementRecord> replacements = new();
        readonly List<StructureDiffRecord> structures = new();
        public StarSystemDelta(StarSystemId id)
        {
            StarSystem = id;
            Destroyed = destroyed.AsReadOnly(); Interacted = interacted.AsReadOnly();
            StorySpawns = stories.AsReadOnly(); Replacements = replacements.AsReadOnly();
            StructureDiffs = structures.AsReadOnly();
        }
        public StarSystemId StarSystem { get; }
        public uint VisitCount { get; private set; }
        public IReadOnlyList<ulong> Destroyed { get; }
        public IReadOnlyList<ulong> Interacted { get; }
        public IReadOnlyList<StorySpawnRecord> StorySpawns { get; }
        public IReadOnlyList<ReplacementRecord> Replacements { get; }
        public IReadOnlyList<StructureDiffRecord> StructureDiffs { get; }
        public bool IsEmpty => VisitCount == 0 && destroyed.Count == 0 && interacted.Count == 0 &&
            stories.Count == 0 && replacements.Count == 0 && structures.Count == 0;
        public void IncrementVisit() => VisitCount = checked(VisitCount + 1);
        public bool MarkDestroyed(ulong bodyKey) => Insert(destroyed, bodyKey);
        public bool MarkInteracted(ulong bodyKey) => Insert(interacted, bodyKey);
        static bool Insert(List<ulong> list, ulong key)
        {
            int index = list.BinarySearch(key);
            if (index >= 0) return false;
            list.Insert(~index, key); return true;
        }
        public int AddStorySpawn(in StorySpawnRecord record)
        {
            if (stories.Count == ushort.MaxValue) throw new InvalidOperationException("Story spawn slot capacity reached.");
            int slot = stories.Count; stories.Add(record); return slot;
        }
        public bool RemoveStorySpawn(int slot)
        {
            if ((uint)slot >= stories.Count || stories[slot].Removed) return false;
            var record = stories[slot]; record.Removed = true; stories[slot] = record; return true;
        }
        public void SetReplacement(ulong bodyKey, ContentId template)
        {
            int index = FindReplacement(bodyKey);
            var record = new ReplacementRecord { BodyKey = bodyKey, Template = template };
            if (index >= 0) replacements[index] = record;
            else replacements.Insert(~index, record);
        }
        public void SetStructure(ulong bodyKey, uint revision, VoxelStructure structure)
        {
            if (structure == null) throw new ArgumentNullException(nameof(structure));
            int index = FindStructure(bodyKey);
            if (index >= 0)
            {
                var record = structures[index];
                if (!ReferenceEquals(record.Structure, structure)) record.Dispose();
                record.Structure = structure; record.Revision = revision;
            }
            else structures.Insert(~index, new StructureDiffRecord { BodyKey = bodyKey, Revision = revision, Structure = structure });
        }
        public bool RemoveStructure(ulong bodyKey)
        {
            int index = FindStructure(bodyKey);
            if (index < 0) return false;
            structures[index].Dispose(); structures.RemoveAt(index); return true;
        }
        public bool TryGetStructure(ulong bodyKey, out StructureDiffRecord record)
        {
            int index = FindStructure(bodyKey);
            record = index < 0 ? null : structures[index]; return index >= 0;
        }
        int FindReplacement(ulong key)
        {
            int lo = 0, hi = replacements.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2; ulong candidate = replacements[mid].BodyKey;
                if (candidate == key) return mid;
                if (candidate < key) lo = mid + 1; else hi = mid - 1;
            }
            return ~lo;
        }
        int FindStructure(ulong key)
        {
            int lo = 0, hi = structures.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2; ulong candidate = structures[mid].BodyKey;
                if (candidate == key) return mid;
                if (candidate < key) lo = mid + 1; else hi = mid - 1;
            }
            return ~lo;
        }
        public void Dispose()
        {
            foreach (var record in structures) record.Dispose();
            structures.Clear(); replacements.Clear(); stories.Clear(); destroyed.Clear(); interacted.Clear(); VisitCount = 0;
        }
    }
}
