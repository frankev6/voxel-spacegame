using Game.Data.Definitions;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace Game.Presentation.Vfx
{
    internal enum FxCounter { EventsOverHardCap, Unresolved, StateKindRejected, QualitySkipped, Culled, BudgetDropped, DebrisSpawned, Count }
    [BurstCompile]
    internal struct FxBatchJob : IJob
    {
        [ReadOnly] public NativeArray<FxEvent> Events;
        [ReadOnly] public NativeArray<FxSlot> Slots;
        [ReadOnly] public NativeHashMap<ulong, int> SlotByHash;
        [ReadOnly] public NativeArray<int> Defaults;
        [ReadOnly] public NativeArray<uint> Palette;
        [ReadOnly] public NativeArray<float3> Anchors;
        public NativeArray<FxGpuEvent> Output;
        public NativeArray<int> Counts, Counters;
        public NativeList<ulong> UnresolvedHashes;
        public float3 Reference;
        public FxQualityBudget Budget;
        public VfxQuality Quality;
        public int DebrisBudget;
        public float DebrisFullDetailDistanceSq, DebrisLifetimeMin, DebrisLifetimeMax;
        public uint Frame;
        public void Execute()
        {
            for (int i = 0; i < Events.Length; i++)
            {
                var e = Events[i]; int kind = (int)e.Kind;
                if (e.Kind == FxKind.Tracer || e.Kind == FxKind.Beam || e.Kind == FxKind.Thruster || e.Kind == FxKind.ShieldShell)
                { Counters[(int)FxCounter.StateKindRejected]++; continue; }
                int slot;
                if (e.Fx.Value == 0 || !SlotByHash.TryGetValue(e.Fx.Value, out slot) || Slots[slot].Kind != e.Kind)
                    slot = kind > 0 && kind < Defaults.Length ? Defaults[kind] : -1;
                if (slot <= 0) { Counters[(int)FxCounter.Unresolved]++; UnresolvedHashes.Add(e.Fx.Value); continue; }
                var s = Slots[slot];
                if (s.MinQuality > Quality) { Counters[(int)FxCounter.QualitySkipped]++; continue; }
                float distanceSq = math.lengthsq(e.Pos - Reference);
                if (!math.all(math.isfinite(e.Pos)) || ((e.Flags & FxEventFlags.PlayerSource) == 0 && distanceSq > s.CullDistanceSq))
                { Counters[(int)FxCounter.Culled]++; continue; }
                float size = (e.Scale > 0 ? e.Scale : 1) * s.Scale;
                int count = math.max(1, (int)math.round(s.ParticlesPerEvent * Budget.ParticleFactor));
                bool debris = e.Kind == FxKind.Debris;
                if (debris && distanceSq > DebrisFullDetailDistanceSq)
                {
                    if (i % 4 != 0) { Counters[(int)FxCounter.Culled]++; continue; }
                    size *= 1.6f;
                }
                if (!debris && distanceSq > s.LodDistanceSq)
                { count = math.max(1, (int)math.ceil(count * s.LodParticleFactor)); size *= 1.6f; }
                if (Counts[slot] >= s.MaxEventsPerFrame || (debris && DebrisBudget == 0))
                { Counters[(int)FxCounter.BudgetDropped]++; continue; }
                uint seed = math.hash(new uint2((uint)i, Frame));
                float lifetime = 1;
                if (debris)
                {
                    DebrisBudget--; Counters[(int)FxCounter.DebrisSpawned]++;
                    lifetime = (e.Flags & FxEventFlags.Collected) != 0 ? FxCollect.Seconds / math.max(DebrisLifetimeMax, FxCollect.Seconds)
                        : math.lerp(DebrisLifetimeMin, DebrisLifetimeMax, (seed >> 8) * (1f / 16777216)) / DebrisLifetimeMax;
                }
                bool palette = (e.Flags & FxEventFlags.PaletteColor) != 0;
                uint color = palette ? Palette[(int)(e.Color & 255)] : e.Color == 0 ? s.DefaultColorRgba8 : e.Color;
                float4 rgba = new float4(color & 255, (color >> 8) & 255, (color >> 16) & 255, color >> 24) / 255f;
                if (!palette) rgba.xyz = math.select(math.pow((rgba.xyz + .055f) / 1.055f, 2.4f), rgba.xyz / 12.92f, rgba.xyz <= .04045f);
                Output[s.BufferOffset + Counts[slot]++] = new FxGpuEvent { position = e.Pos - Anchors[slot], size = size,
                    direction = math.normalizesafe(e.Dir, new float3(0, 1, 0)), lifetimeScale = lifetime,
                    velocity = e.Vel, seed = seed, color = rgba, particleCount = (uint)count, kind = (uint)e.Kind };
            }
        }
    }
}
