using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Game.Voxel.PresentationStreams;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
namespace Game.Presentation.Vfx
{
    // Internal sink boundary also permits the spec's fake rig without allocating graphics resources.
    internal interface IFxUploadTarget
    {
        FxUploadData UploadData { get; }
        NativeList<FxEvent> PendingImpacts { get; }
        FxStats Stats { get; }
        void Submit(int slot, NativeArray<FxGpuEvent> events, int start, int count);
    }
    internal struct FxUploadData
    {
        public NativeArray<FxSlot> Slots;
        public NativeHashMap<ulong, int> Hashes;
        public NativeArray<int> Defaults;
        public NativeArray<uint> Palette;
        public NativeArray<float3> Anchors;
        public VfxQuality Quality;
        public FxQualityBudget Budget;
        public float DebrisFullDetailDistance, DebrisLifetimeMin, DebrisLifetimeMax;
    }
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    public partial class FxUploadSystem : SystemBase, IFxRigConsumer
    {
        static readonly ProfilerMarker DrainMarker = new ProfilerMarker(ProfilerCategory.Scripts, "FxUploadSystem.Drain");
        static readonly ProfilerMarker BatchMarker = new ProfilerMarker(ProfilerCategory.Scripts, "FxUploadSystem.Batch");
        static readonly ProfilerMarker SubmitMarker = new ProfilerMarker(ProfilerCategory.Scripts, "FxUploadSystem.Submit");
        NativeList<FxEvent> drained;
        int drainedFrame = -1;
        NativeList<ulong> unresolved;
        NativeHashSet<ulong> warned;
        NativeArray<FxGpuEvent> gpu;
        NativeArray<int> counts, counters, liveCounts;
        NativeArray<double> liveTimes;
        IFxUploadTarget target;
        int ringNext;
        uint frame, lastShiftSequence, clearRevision;
        bool hasShiftSequence;
        bool hardCapWarned, stateWarned;
        readonly FxStats unboundStats = new FxStats();
        public FxRig Rig { get => target as FxRig; set => Bind(value); }
        public FxStats Stats => target?.Stats ?? unboundStats;
        // CAM-008: synchronous drain/batch jobs have finished before the camera reads this borrowed view.
        // Never retain it or schedule jobs with it; the next upload clears/reuses the owning list.
        internal NativeArray<FxEvent>.ReadOnly FrameEvents => drained.IsCreated && drainedFrame == UnityEngine.Time.frameCount
            ? drained.AsArray().AsReadOnly() : default;
        protected override void OnCreate()
        {
            drained = new NativeList<FxEvent>(32768, Allocator.Persistent);
            unresolved = new NativeList<ulong>(65536, Allocator.Persistent);
            warned = new NativeHashSet<ulong>(65536, Allocator.Persistent);
            counters = new NativeArray<int>((int)FxCounter.Count, Allocator.Persistent);
            liveCounts = new NativeArray<int>(1024, Allocator.Persistent); liveTimes = new NativeArray<double>(1024, Allocator.Persistent);
            RequireForUpdate<FxEventQueue>();
        }
        internal void Bind(IFxUploadTarget value)
        {
            if (ReferenceEquals(target, value)) return;
            if (gpu.IsCreated) gpu.Dispose(); if (counts.IsCreated) counts.Dispose();
            target = value; ringNext = 0; hasShiftSequence = false; clearRevision = (value as FxRig)?.ClearRevision ?? 0;
            for (int i = 0; i < liveCounts.Length; i++) liveCounts[i] = 0;
            if (value == null) return;
            var slots = value.UploadData.Slots; int capacity = 0;
            for (int i = 1; i < slots.Length; i++) capacity = math.max(capacity, slots[i].BufferOffset + slots[i].MaxEventsPerFrame);
            gpu = new NativeArray<FxGpuEvent>(math.max(1, capacity), Allocator.Persistent);
            counts = new NativeArray<int>(slots.Length, Allocator.Persistent);
        }
        protected override void OnUpdate()
        {
            Reset(Stats); for (int i = 0; i < counters.Length; i++) counters[i] = 0;
            unresolved.Clear(); frame++;
            var stream = SystemAPI.GetSingleton<FxEventQueue>(); stream.Access.CompleteAccess();
            using (DrainMarker.Auto())
            {
                new FxDrainJob { Queue = stream.Queue, Drained = drained, Counters = counters }.Run();
                if (target != null)
                {
                    var pending = target.PendingImpacts;
                    int keep = math.min(pending.Length, FxDrainJob.HardCap - drained.Length);
                    for (int i = 0; i < keep; i++) drained.Add(pending[i]);
                    counters[(int)FxCounter.EventsOverHardCap] += pending.Length - keep; pending.Clear();
                }
            }
            int over = counters[(int)FxCounter.EventsOverHardCap];
            drainedFrame = UnityEngine.Time.frameCount;
            if (over > 0 && !hardCapWarned) { hardCapWarned = true; GameLog.WarnOnce("Vfx.HardCap", "Vfx", "FX events exceeded the 65536 frame cap; excess events discarded."); }
            var stats = Stats; stats.EventsDrained = drained.Length; stats.EventsOverHardCap = over;
            if (target == null) { stats.EventsOverHardCap += drained.Length; return; }
            stats.Bound = true;
            var data = target.UploadData;
            double now = SystemAPI.Time.ElapsedTime;
            if (Rig != null)
            {
                Rig.Now = now; ApplyOriginShift();
                if (Rig.ClearRevision != clearRevision)
                { clearRevision = Rig.ClearRevision; for (int i = 0; i < liveCounts.Length; i++) liveCounts[i] = 0; ringNext = 0; }
                Rig.ResetIdleAnchors(now);
            }
            int live = 0;
            for (int i = 0; i < liveCounts.Length; i++)
            { if (now - liveTimes[i] >= data.DebrisLifetimeMax || now < liveTimes[i]) liveCounts[i] = 0; live += liveCounts[i]; }
            float3 reference = float3.zero;
            foreach (var transform in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<PlayerShip>().WithNone<Parent>())
            { reference = transform.ValueRO.Position; break; }
            for (int i = 0; i < counts.Length; i++) counts[i] = 0;
            using (BatchMarker.Auto()) new FxBatchJob { Events = drained.AsArray(), Slots = data.Slots, SlotByHash = data.Hashes,
                Defaults = data.Defaults, Palette = data.Palette, Anchors = data.Anchors, Output = gpu, Counts = counts, Counters = counters,
                UnresolvedHashes = unresolved, Reference = reference, Quality = data.Quality, Budget = data.Budget,
                DebrisBudget = math.max(0, math.min(data.Budget.DebrisSpawnPerFrame, data.Budget.DebrisLiveCap - live)),
                DebrisFullDetailDistanceSq = data.DebrisFullDetailDistance * data.DebrisFullDetailDistance,
                DebrisLifetimeMin = data.DebrisLifetimeMin, DebrisLifetimeMax = data.DebrisLifetimeMax, Frame = frame }.Run();
            stats.Unresolved = counters[(int)FxCounter.Unresolved]; stats.StateKindRejected = counters[(int)FxCounter.StateKindRejected];
            stats.QualitySkipped = counters[(int)FxCounter.QualitySkipped]; stats.Culled = counters[(int)FxCounter.Culled];
            stats.BudgetDropped = counters[(int)FxCounter.BudgetDropped]; stats.DebrisSpawned = counters[(int)FxCounter.DebrisSpawned];
            using (SubmitMarker.Auto()) for (int i = 1; i < counts.Length; i++) if (counts[i] > 0) target.Submit(i, gpu, data.Slots[i].BufferOffset, counts[i]);
            // If >1024 submissions fit inside a lifetime, merge the oldest bucket and extend its expiry.
            // This can overestimate live debris, but never frees capacity for particles that may still be alive.
            if (stats.DebrisSpawned > 0)
            { liveCounts[ringNext] += stats.DebrisSpawned; liveTimes[ringNext] = now; ringNext = (ringNext + 1) % liveCounts.Length; }
            stats.DebrisLiveEstimate = live + stats.DebrisSpawned;
            if (SystemAPI.HasSingleton<ChunkDissolveStats>())
            { EntityManager.CompleteDependencyBeforeRO<ChunkDissolveStats>(); stats.DissolvingChunks = SystemAPI.GetSingleton<ChunkDissolveStats>().Count.Value; }
            if (stats.StateKindRejected > 0 && !stateWarned) { stateWarned = true; GameLog.WarnOnce("Vfx.StateKind", "Vfx", "State effect kinds cannot be submitted as events."); }
            for (int i = 0; i < unresolved.Length; i++) if (warned.Count < 65536 && warned.Add(unresolved[i]))
                GameLog.WarnOnce("Vfx.Unresolved." + unresolved[i], "Vfx", "No effect slot or default resolves hash " + unresolved[i]);
        }
        void ApplyOriginShift()
        {
            if (Rig == null || !SystemAPI.TryGetSingleton<OriginShiftRecord>(out var shift)) return;
            if (!hasShiftSequence) { hasShiftSequence = true; lastShiftSequence = shift.Sequence; return; }
            if (shift.Sequence == lastShiftSequence) return;
            bool discontinuity = shift.StarSystemChanged != 0 || shift.Sequence - lastShiftSequence != 1 || math.lengthsq(shift.Delta) > 20000f * 20000f;
            lastShiftSequence = shift.Sequence;
            if (discontinuity) Rig.ClearAll(true); else Rig.ShiftAnchors(shift.Delta);
        }
        static void Reset(FxStats s)
        {
            s.EventsDrained = s.EventsOverHardCap = s.Unresolved = s.StateKindRejected = s.QualitySkipped = s.Culled = s.BudgetDropped = 0;
            s.BatchesSubmitted = s.PooledPlayed = s.DebrisSpawned = s.DebrisLiveEstimate = 0;
            s.TracersDrawn = s.BeamsDrawn = s.PlumesDrawn = s.ShieldsDrawn = s.DissolvingChunks = 0; s.Bound = false;
        }
        protected override void OnDestroy()
        {
            Bind(null); drained.Dispose(); unresolved.Dispose(); warned.Dispose(); counters.Dispose(); liveCounts.Dispose(); liveTimes.Dispose();
        }
    }
}
