using System;
using Game.Data.Definitions;
using Game.Presentation.Vfx;
using Game.Voxel.PresentationStreams;
using Unity.Collections;
using Unity.Mathematics;
namespace Game.Tests.EditMode.Vfx
{
    internal sealed class FxUploadFixture : IFxUploadTarget, IDisposable
    {
        public FxUploadData Data;
        public FxUploadData UploadData => Data;
        public NativeList<FxEvent> PendingImpacts { get; }
        public FxStats Stats { get; } = new FxStats();
        public NativeArray<FxGpuEvent> Output;
        public NativeArray<int> Counts, Counters;
        public NativeList<ulong> Unresolved;
        public int Submitted;
        public float PositionSum;
        public FxUploadFixture(FxKind kind = FxKind.HitKinetic)
        {
            Data = new FxUploadData { Slots = new NativeArray<FxSlot>(2, Allocator.Persistent), Hashes = new NativeHashMap<ulong,int>(1, Allocator.Persistent),
                Defaults = new NativeArray<int>(14, Allocator.Persistent), Palette = new NativeArray<uint>(256, Allocator.Persistent),
                Anchors = new NativeArray<float3>(2, Allocator.Persistent), Quality = VfxQuality.High,
                Budget = new FxQualityBudget { ParticleFactor = 1, DebrisLiveCap = 50000, DebrisSpawnPerFrame = 20000 },
                DebrisFullDetailDistance = 150, DebrisLifetimeMin = 3, DebrisLifetimeMax = 6 };
            for (int i = 0; i < 14; i++) Data.Defaults[i] = -1;
            Data.Defaults[(int)kind] = 1; Data.Hashes.Add(123, 1);
            Data.Slots[1] = new FxSlot { Kind = kind, Mode = FxMode.Batched, Scale = 1, DefaultColorRgba8 = uint.MaxValue,
                ParticlesPerEvent = kind == FxKind.Debris ? 1 : 8, MaxEventsPerFrame = 64,
                CullDistanceSq = 1000000, LodDistanceSq = 90000, LodParticleFactor = .25f };
            PendingImpacts = new NativeList<FxEvent>(1024, Allocator.Persistent);
            Output = new NativeArray<FxGpuEvent>(64, Allocator.Persistent); Counts = new NativeArray<int>(2, Allocator.Persistent);
            Counters = new NativeArray<int>((int)FxCounter.Count, Allocator.Persistent); Unresolved = new NativeList<ulong>(64, Allocator.Persistent);
        }
        public FxBatchJob Job(NativeArray<FxEvent> events, int debrisBudget = 20000) => new FxBatchJob {
            Events = events, Slots = Data.Slots, SlotByHash = Data.Hashes, Defaults = Data.Defaults, Palette = Data.Palette, Anchors = Data.Anchors,
            Output = Output, Counts = Counts, Counters = Counters, UnresolvedHashes = Unresolved, Budget = Data.Budget, Quality = Data.Quality,
            DebrisBudget = debrisBudget, DebrisFullDetailDistanceSq = 22500, DebrisLifetimeMin = 3, DebrisLifetimeMax = 6 };
        public void Submit(int slot, NativeArray<FxGpuEvent> events, int start, int count)
        { Submitted += count; Stats.BatchesSubmitted++; for (int i = 0; i < count; i++) PositionSum += events[start + i].position.x; }
        public void Dispose()
        {
            Data.Slots.Dispose(); Data.Hashes.Dispose(); Data.Defaults.Dispose(); Data.Palette.Dispose(); Data.Anchors.Dispose();
            PendingImpacts.Dispose(); Output.Dispose(); Counts.Dispose(); Counters.Dispose(); Unresolved.Dispose();
        }
    }
}
