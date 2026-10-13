using Game.Core;
using Game.Data.Definitions;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Presentation.Audio
{
    /// <summary>SPEC-64 §5.3: cull, coalesce, apply cooldown and sort bounded presentation candidates.</summary>
    [BurstCompile] internal struct SfxCoalesceJob : IJob
    {
        [ReadOnly] public NativeList<SfxEvent> Events;
        [ReadOnly] public NativeHashMap<ulong, int> Slots;
        [ReadOnly] public NativeArray<SfxCueInfo> Cues;
        [ReadOnly] public NativeHashMap<ulong, double> LastStart;
        public NativeList<SfxCandidate> Candidates;
        public NativeHashMap<ulong, int> Groups;
        public NativeList<ulong> Unknown;
        public NativeArray<int> Counters;
        public float3 Listener;
        public double Now;
        public float CombatRadius;
        public void Execute()
        {
            Candidates.Clear(); Groups.Clear(); Unknown.Clear();
            for (int i = 0; i < Events.Length; i++)
            {
                var value = Events[i];
                if (value.Cue.Value == 0 || !Slots.TryGetValue(value.Cue.Value, out int slot))
                {
                    Counters[(int)SfxCounter.Unresolved]++;
                    if (value.Cue.Value != 0) Unknown.Add(value.Cue.Value);
                    continue;
                }
                var cue = Cues[slot];
                if (!math.all(math.isfinite(value.Pos))) { Counters[(int)SfxCounter.Culled]++; continue; }
                if (!math.isfinite(value.Volume) || value.Volume <= .001f) { Counters[(int)SfxCounter.Silent]++; continue; }
                if (cue.SpatialBlend > 0 && (value.Flags & SfxEventFlags.PlayerSource) == 0 && math.distance(value.Pos, Listener) > cue.MaxDistance)
                { Counters[(int)SfxCounter.Culled]++; continue; }
                ulong key = GroupKey(slot, cue, value.Pos, i);
                if (Groups.TryGetValue(key, out int at))
                {
                    var candidate = Candidates[at]; candidate.Count++; candidate.Flags |= value.Flags;
                    if (value.Volume > candidate.MaxVolume) { candidate.MaxVolume = value.Volume; candidate.Pos = value.Pos; }
                    Candidates[at] = candidate;
                }
                else
                {
                    Groups.Add(key, Candidates.Length);
                    Candidates.Add(new SfxCandidate { Slot = slot, Pos = value.Pos, MaxVolume = value.Volume,
                        Count = 1, GroupKey = key, Flags = value.Flags, Order = Candidates.Length });
                }
            }
            Counters[(int)SfxCounter.Groups] = Candidates.Length;
            int kept = 0;
            for (int i = 0; i < Candidates.Length; i++)
            {
                var candidate = Candidates[i]; var cue = Cues[candidate.Slot];
                candidate.FinalVolume = math.min(1, candidate.MaxVolume * (1 + cue.CoalesceGain * math.log2(candidate.Count)));
                if (LastStart.TryGetValue(candidate.GroupKey, out double last) && Now - last < cue.Cooldown)
                { Counters[(int)SfxCounter.CooldownDropped]++; continue; }
                float distance = math.distance(candidate.Pos, Listener);
                candidate.Audibility = candidate.FinalVolume * cue.Volume * (cue.SpatialBlend > 0 ? math.saturate(cue.MinDistance / math.max(distance, cue.MinDistance)) : 1);
                if (cue.CombatSignal && distance <= CombatRadius) Counters[(int)SfxCounter.CombatSignals] += candidate.Count;
                Candidates[kept++] = candidate;
            }
            Candidates.ResizeUninitialized(kept);
            Candidates.AsArray().Sort(new CandidateOrder { Cues = Cues });
        }
        internal static ulong GroupKey(int slot, in SfxCueInfo cue, float3 position, int order)
        {
            if (cue.Coalesce == CoalesceMode.None) return StableHash.Combine((ulong)slot, (ulong)order | (1UL << 63));
            if (cue.Coalesce != CoalesceMode.PerCell || cue.SpatialBlend <= 0) return (ulong)slot;
            int3 cell = (int3)math.floor(position / cue.CoalesceCellSize);
            ulong hash = StableHash.Combine(StableHash.Combine((uint)cell.x, (uint)cell.y), (uint)cell.z);
            return StableHash.Combine((ulong)slot, hash);
        }
        struct CandidateOrder : System.Collections.Generic.IComparer<SfxCandidate>
        {
            [ReadOnly] public NativeArray<SfxCueInfo> Cues;
            public int Compare(SfxCandidate a, SfxCandidate b)
            {
                int c = Cues[a.Slot].Priority.CompareTo(Cues[b.Slot].Priority);
                if (c == 0) c = ((byte)(b.Flags & SfxEventFlags.PlayerSource)).CompareTo((byte)(a.Flags & SfxEventFlags.PlayerSource));
                if (c == 0) c = b.Audibility.CompareTo(a.Audibility);
                return c != 0 ? c : a.Order.CompareTo(b.Order);
            }
        }
    }
}
