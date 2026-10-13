using Game.Core;
using Game.Core.Systems;
using Game.Voxel.PresentationStreams;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

namespace Game.Presentation.Audio
{
    /// <summary>SPEC-64 §5.3: bounded, synchronous presentation drain, including while unbound.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    public partial class AudioEventDrainSystem : SystemBase
    {
        static readonly ProfilerMarker DrainMarker = new ProfilerMarker("AudioEventDrainSystem.Drain");
        static readonly ProfilerMarker CoalesceMarker = new ProfilerMarker("AudioEventDrainSystem.Coalesce");
        static readonly ProfilerMarker AllocateMarker = new ProfilerMarker("AudioEventDrainSystem.Allocate");
        NativeList<SfxEvent> drained;
        NativeList<SfxCandidate> candidates;
        NativeList<ulong> unknown, expired;
        NativeHashSet<ulong> warned;
        NativeHashMap<ulong, int> groups;
        NativeHashMap<ulong, double> lastStart;
        NativeArray<int> counters;
        readonly AudioStats unboundStats = new AudioStats();
        internal AudioService Service { get; private set; }
        public AudioStats Stats => Service?.Stats ?? unboundStats;

        protected override void OnCreate()
        {
            drained = new NativeList<SfxEvent>(4096, Allocator.Persistent);
            candidates = new NativeList<SfxCandidate>(4096, Allocator.Persistent);
            unknown = new NativeList<ulong>(4096, Allocator.Persistent);
            expired = new NativeList<ulong>(1024, Allocator.Persistent);
            warned = new NativeHashSet<ulong>(1024, Allocator.Persistent);
            groups = new NativeHashMap<ulong, int>(4096, Allocator.Persistent);
            lastStart = new NativeHashMap<ulong, double>(1024, Allocator.Persistent);
            counters = new NativeArray<int>((int)SfxCounter.Count, Allocator.Persistent);
            RequireForUpdate<SfxEventQueue>();
        }
        internal void Bind(AudioService service)
        {
            Service = service; lastStart.Clear();
        }
        protected override void OnUpdate()
        {
            // All producers take this singleton RW and publish their jobs in state.Dependency.
            // Complete after acquiring RW: the native queue alias must not outlive any producer.
            ref var queue = ref SystemAPI.GetSingletonRW<SfxEventQueue>().ValueRW;
            CompleteDependency();
            var stats = Stats;
            stats.Started = stats.Stolen = stats.RejectedNoVoice = stats.ActiveSfxVoices = 0;
            stats.Unresolved = stats.Silent = stats.Culled = stats.Groups = stats.CooldownDropped = stats.CombatSignals = 0;
            stats.Bound = Service != null;
            using (DrainMarker.Auto()) new SfxDrainJob { Queue = queue.Queue, Drained = drained, Counters = counters }.Run();
            stats.EventsDrained = counters[(int)SfxCounter.Drained]; stats.EventsOverHardCap = counters[(int)SfxCounter.OverCap];
            if (Service == null) return;
            // TODO(AUD-012): shift or stop spatial voices on origin and system changes.
            double now = Time.unscaledTimeAsDouble;
            expired.Clear();
            foreach (var pair in lastStart) if (now - pair.Value > 2) expired.Add(pair.Key);
            for (int i = 0; i < expired.Length; i++) lastStart.Remove(expired[i]);
            using (CoalesceMarker.Auto()) new SfxCoalesceJob
            {
                Events = drained, Slots = Service.CueSlots, Cues = Service.CueInfo, LastStart = lastStart,
                Candidates = candidates, Groups = groups, Unknown = unknown, Counters = counters,
                Listener = Service.ListenerPosition, Now = now, CombatRadius = Service.CombatRadius
            }.Run();
            for (int i = 0; i < unknown.Length; i++) if (warned.Add(unknown[i]))
                GameLog.WarnOnce("Audio.UnknownHash." + unknown[i], "Audio", "Unknown audio cue hash: " + unknown[i]);
            Service.RefreshSfxVoices();
            using (AllocateMarker.Auto())
            {
                int started = 0;
                for (int i = 0; i < candidates.Length && started < 16; i++)
                {
                    var candidate = candidates[i];
                    if (!Service.TryStartSfx(candidate, now)) continue;
                    lastStart[candidate.GroupKey] = now; started++;
                }
            }
            stats.Unresolved = counters[(int)SfxCounter.Unresolved]; stats.Silent = counters[(int)SfxCounter.Silent];
            stats.Culled = counters[(int)SfxCounter.Culled]; stats.Groups = counters[(int)SfxCounter.Groups];
            stats.CooldownDropped = counters[(int)SfxCounter.CooldownDropped]; stats.CombatSignals = counters[(int)SfxCounter.CombatSignals];
            stats.ActiveSfxVoices = Service.ActiveSfxCount;
            // TODO(AUD-008): feed this frame's CombatSignals to the music director.
        }
        protected override void OnDestroy()
        {
            CompleteDependency(); Service = null;
            drained.Dispose(); candidates.Dispose(); unknown.Dispose(); expired.Dispose(); warned.Dispose();
            groups.Dispose(); lastStart.Dispose(); counters.Dispose();
        }
    }
}
