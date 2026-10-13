using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

namespace Game.Presentation.Vfx
{
    // VFX-003/005–008/012 systems implement this binding hook as they land. No placeholder simulation systems are created.
    internal interface IFxRigConsumer { FxRig Rig { get; set; } }

    /// <summary>SPEC-63 §6.1: session resource owner; all objects and buffers are allocated at Create.</summary>
    public sealed class FxRig : MonoBehaviour, IFxUploadTarget
    {
        static readonly Dictionary<World, FxRig> Bindings = new Dictionary<World, FxRig>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBindings() => Bindings.Clear();
        static readonly int QualityId = Shader.PropertyToID("QualityFactor");
        static readonly int EventsId = Shader.PropertyToID("FxEvents");
        static readonly int CountId = Shader.PropertyToID("FxEventCount");
        static readonly int ParticlesId = Shader.PropertyToID("ParticlesPerEvent");
        static readonly int BatchEventId = Shader.PropertyToID("OnFxBatch");
        static readonly int PlayEventId = Shader.PropertyToID("OnFxPlay");
        static readonly int SpawnCountId = Shader.PropertyToID("spawnCount");
        static readonly int DirectionId = Shader.PropertyToID("direction");
        static readonly int VelocityId = Shader.PropertyToID("velocity");
        static readonly int ColorId = Shader.PropertyToID("color");
        static readonly int AlphaId = Shader.PropertyToID("alpha");
        static readonly int SizeId = Shader.PropertyToID("size");
        static readonly int LifetimeId = Shader.PropertyToID("DebrisLifetimeMax");
        internal NativeArray<FxSlot> Slots;
        internal NativeHashMap<ulong, int> SlotByHash;
        internal NativeArray<int> DefaultSlotByKind;
        internal NativeArray<uint> Palette;
        internal NativeArray<float3> AnchorPositions;
        internal NativeList<Game.Voxel.PresentationStreams.FxEvent> PendingImpacts;
        internal VfxConfigDefinition Config { get; private set; }
        FxUploadData IFxUploadTarget.UploadData => new FxUploadData { Slots = Slots, Hashes = SlotByHash, Defaults = DefaultSlotByKind,
            Palette = Palette, Anchors = AnchorPositions, Quality = Quality, Budget = Budget,
            DebrisFullDetailDistance = Config.DebrisFullDetailDistance, DebrisLifetimeMin = Config.DebrisLifetimeMin, DebrisLifetimeMax = Config.DebrisLifetimeMax };
        NativeList<Game.Voxel.PresentationStreams.FxEvent> IFxUploadTarget.PendingImpacts => PendingImpacts;
        internal FxQualityBudget Budget => Config.Budget(Quality);
        internal FxInstanceBatch<TracerInstance> Tracers { get; private set; }
        internal FxInstanceBatch<BeamInstance> Beams { get; private set; }
        internal FxInstanceBatch<PlumeInstance> Plumes { get; private set; }
        internal FxInstanceBatch<ShieldInstance> Shields { get; private set; }
        readonly FxStats stats = new FxStats();
        SlotResources[] resources;
        World boundWorld;
        bool disposed;
        public VfxQuality Quality { get; private set; }
        public FxStats Stats { get { stats.Bound = boundWorld != null && boundWorld.IsCreated; return stats; } }
        internal long TotalBufferBytes
        {
            get { long bytes = 0; if (resources != null) foreach (var r in resources) if (r != null) bytes += r.Buffer == null ? r.Instances?.BufferBytes ?? 0 : (long)r.Buffer.count * r.Buffer.stride; return bytes; }
        }
        internal GraphicsBuffer EventBuffer(int slot) => resources[slot]?.Buffer;
        internal int LiveBufferCount
        {
            get { int count = 0; if (resources != null) foreach (var r in resources) if (r != null && (r.Buffer != null || (r.Instances?.BufferBytes ?? 0) > 0)) count++; return count; }
        }
        sealed class SlotResources : IDisposable
        {
            public VisualEffect[] Effects;
            public GraphicsBuffer Buffer;
            public IFxInstanceBatch Instances;
            public VFXEventAttribute[] Attributes;
            public double[] EndTimes;
            public int Next;
            public void Dispose()
            {
                Buffer?.Dispose(); Buffer = null; Instances?.Dispose(); Instances = null;
                if (Attributes != null) foreach (var attribute in Attributes) attribute?.Dispose();
                if (Effects != null) foreach (var effect in Effects) if (effect != null) { effect.Stop(); Destroy(effect.gameObject); }
                Effects = null; Attributes = null; EndTimes = null;
            }
        }
        public static FxRig Create(Transform parent, VfxConfigDefinition config, IReadOnlyList<FxDefinition> definitions, Color32[] cellPalette, VfxQuality quality)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var owner = new GameObject("FxRig"); owner.transform.SetParent(parent, false); var rig = owner.AddComponent<FxRig>();
            try { rig.Initialize(config, definitions, cellPalette, quality); return rig; }
            catch { rig.Dispose(); Destroy(owner); throw; }
        }
        void Initialize(VfxConfigDefinition config, IReadOnlyList<FxDefinition> definitions, Color32[] colors, VfxQuality quality)
        {
            Config = config; Quality = quality;
            resources = new SlotResources[definitions.Count + 1];
            Slots = new NativeArray<FxSlot>(resources.Length, Allocator.Persistent);
            AnchorPositions = new NativeArray<float3>(resources.Length, Allocator.Persistent);
            SlotByHash = new NativeHashMap<ulong, int>(Math.Max(1, definitions.Count), Allocator.Persistent);
            DefaultSlotByKind = new NativeArray<int>(14, Allocator.Persistent); for (int i = 0; i < DefaultSlotByKind.Length; i++) DefaultSlotByKind[i] = -1;
            Palette = new NativeArray<uint>(256, Allocator.Persistent);
            PendingImpacts = new NativeList<Game.Voxel.PresentationStreams.FxEvent>(1024, Allocator.Persistent);
            var palette = colors != null && colors.Length == 256 ? colors : config.FallbackCellPalette;
            for (int i = 0; i < 256; i++)
            {
                Color c = palette != null && palette.Length == 256 ? palette[i] : new Color32((byte)i, (byte)i, (byte)i, 255);
                Palette[i] = Pack((Color32)c.linear);
            }
            int offset = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i]; if (def == null) continue; int slot = i + 1;
                var r = new SlotResources(); resources[slot] = r;
                if (def.Mode == FxMode.Instanced)
                {
                    if (def.Mesh == null || def.Material == null) { Reject(def, "mesh/material"); continue; }
                    switch (def.Kind)
                    {
                        case FxKind.Tracer: r.Instances = new FxInstanceBatch<TracerInstance>(def.Mesh, def.Material, config.High.MaxTracers); break;
                        case FxKind.Beam: r.Instances = new FxInstanceBatch<BeamInstance>(def.Mesh, def.Material, config.High.MaxBeams); break;
                        case FxKind.Thruster: r.Instances = new FxInstanceBatch<PlumeInstance>(def.Mesh, def.Material, config.High.MaxPlumes); break;
                        case FxKind.ShieldShell: r.Instances = new FxInstanceBatch<ShieldInstance>(def.Mesh, def.Material, config.High.MaxShields); break;
                        default: Reject(def, "instanced kind"); continue;
                    }
                }
                else
                {
                    if (def.Graph == null) { Reject(def, "graph"); continue; }
                    int count = def.Mode == FxMode.Pooled ? def.PoolSize : 1;
                    r.Effects = new VisualEffect[count]; r.Attributes = new VFXEventAttribute[count]; r.EndTimes = new double[count];
                    bool valid = true;
                    for (int n = 0; n < count; n++)
                    {
                        var child = new GameObject("Fx_" + def.Id.Value + (count > 1 ? "_" + n : "")); child.transform.SetParent(transform, false);
                        var effect = child.AddComponent<VisualEffect>(); r.Effects[n] = effect; effect.visualEffectAsset = def.Graph;
                        effect.playRate = 1; effect.Stop();
                        string missing = MissingProperty(effect, def.Mode);
                        if (missing != null) { Reject(def, missing); valid = false; break; }
                        effect.SetFloat(QualityId, config.Budget(quality).ParticleFactor);
                        if (def.Kind == FxKind.Debris && effect.HasFloat(LifetimeId)) effect.SetFloat(LifetimeId, config.DebrisLifetimeMax);
                        r.Attributes[n] = effect.CreateVFXEventAttribute();
                    }
                    if (!valid) { r.Dispose(); continue; }
                    if (def.Mode == FxMode.Batched)
                    {
                        r.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Math.Max(1, def.MaxEventsPerFrame), 80);
                        r.Effects[0].SetGraphicsBuffer(EventsId, r.Buffer);
                        r.Effects[0].SetInt(CountId, 0); r.Effects[0].SetInt(ParticlesId, def.ParticlesPerEvent);
                    }
                }
                Slots[slot] = new FxSlot { Kind = def.Kind, Mode = def.Mode, Scale = def.Scale, DefaultColorRgba8 = Pack(def.DefaultColor),
                    ParticlesPerEvent = def.ParticlesPerEvent, MaxEventsPerFrame = def.MaxEventsPerFrame, BufferOffset = offset, MaxLifetime = def.MaxLifetime,
                    CullDistanceSq = def.CullDistance * def.CullDistance, LodDistanceSq = def.LodDistance * def.LodDistance,
                    LodParticleFactor = def.LodParticleFactor, MinQuality = def.MinQuality };
                offset += def.MaxEventsPerFrame; SlotByHash.TryAdd(def.Id.Hash.Value, slot);
            }
            for (int k = 1; k < DefaultSlotByKind.Length && k < config.Defaults.Length; k++)
            {
                var def = config.Defaults[k];
                if (def != null && SlotByHash.TryGetValue(def.Id.Hash.Value, out int slot) && Slots[slot].Kind == (FxKind)k) DefaultSlotByKind[k] = slot;
            }
            Tracers = DefaultInstances<TracerInstance>(FxKind.Tracer); Beams = DefaultInstances<BeamInstance>(FxKind.Beam);
            Plumes = DefaultInstances<PlumeInstance>(FxKind.Thruster); Shields = DefaultInstances<ShieldInstance>(FxKind.ShieldShell);
        }
        FxInstanceBatch<T> DefaultInstances<T>(FxKind kind) where T : unmanaged
        { int slot = DefaultSlotByKind[(int)kind]; return slot < 0 ? null : resources[slot].Instances as FxInstanceBatch<T>; }
        internal static uint Pack(Color32 c) => c.r | ((uint)c.g << 8) | ((uint)c.b << 16) | ((uint)c.a << 24);
        static string MissingProperty(VisualEffect effect, FxMode mode)
        {
            if (mode == FxMode.Batched)
            {
                if (!effect.HasGraphicsBuffer(EventsId)) return "FxEvents";
                if (!effect.HasInt(CountId)) return "FxEventCount";
                if (!effect.HasInt(ParticlesId)) return "ParticlesPerEvent";
            }
            return effect.HasFloat(QualityId) ? null : "QualityFactor";
        }
        static void Reject(FxDefinition definition, string property) => GameLog.Error("Vfx", $"{definition.Id.Value}: graph '{(definition.Graph == null ? "null" : definition.Graph.name)}' missing/invalid {property}; slot skipped.");
        public void BindTo(World world)
        {
            if (disposed) throw new ObjectDisposedException(nameof(FxRig));
            if (world == null || !world.IsCreated) throw new ArgumentException("FX binding requires a live World.", nameof(world));
            if (Bindings.TryGetValue(world, out var previous) && previous != this) previous.Unbind(world);
            if (boundWorld != null && boundWorld != world) Unbind(boundWorld);
            using var q = world.EntityManager.CreateEntityQuery(typeof(VfxRuntimeConfig));
            var config = new VfxRuntimeConfig { ChunkDissolveSeconds = Config.ChunkDissolveSeconds };
            if (q.IsEmpty) world.EntityManager.CreateSingleton(config); else q.SetSingleton(config);
            Bindings[world] = this; boundWorld = world; stats.Bound = true;
            foreach (var system in world.Systems) if (system is IFxRigConsumer consumer) consumer.Rig = this;
        }
        public void Unbind(World world)
        {
            if (world == null || boundWorld != world) return;
            if (world.IsCreated)
            {
                foreach (var system in world.Systems) if (system is IFxRigConsumer consumer && consumer.Rig == this) consumer.Rig = null;
                using var q = world.EntityManager.CreateEntityQuery(typeof(VfxRuntimeConfig)); if (!q.IsEmpty) world.EntityManager.DestroyEntity(q);
            }
            if (Bindings.TryGetValue(world, out var rig) && rig == this) Bindings.Remove(world);
            boundWorld = null; stats.Bound = false;
        }
        public void SetQuality(VfxQuality q)
        {
            Quality = q; if (disposed || resources == null) return;
            foreach (var r in resources) if (r?.Effects != null) foreach (var effect in r.Effects) if (effect != null) effect.SetFloat(QualityId, Config.Budget(q).ParticleFactor);
        }
        public void Submit(int slot, NativeArray<FxGpuEvent> events, int start, int count)
        {
            if (disposed || count == 0) return;
            if (slot <= 0 || slot >= Slots.Length || start < 0 || count < 0 || count > events.Length - start)
                throw new ArgumentOutOfRangeException(nameof(slot));
            var r = resources[slot]; var definition = Slots[slot];
            if (r?.Effects == null) return;
            if (definition.Mode == FxMode.Batched)
            {
                if (count > definition.MaxEventsPerFrame) throw new ArgumentOutOfRangeException(nameof(count));
                r.Buffer.SetData(events, start, 0, count);
                var effect = r.Effects[0]; effect.SetInt(CountId, count); effect.SetInt(ParticlesId, definition.ParticlesPerEvent);
                effect.SetFloat(QualityId, Budget.ParticleFactor);
                var attribute = r.Attributes[0]; attribute.SetFloat(SpawnCountId, count * definition.ParticlesPerEvent);
                effect.SendEvent(BatchEventId, attribute); stats.BatchesSubmitted++;
            }
            else
            {
                int accepted = Math.Min(count, r.Effects.Length); stats.BudgetDropped += count - accepted;
                for (int i = 0; i < accepted; i++)
                {
                    int index = r.Next; r.Next = (r.Next + 1) % r.Effects.Length;
                    var e = events[start + i]; var effect = r.Effects[index]; var attribute = r.Attributes[index];
                    // Reuse steals the oldest instance rather than adding another live effect to that pool slot.
                    effect.Reinit(); effect.Stop(); effect.transform.position = e.position + (Vector3)AnchorPositions[slot];
                    attribute.SetVector3(DirectionId, e.direction); attribute.SetVector3(VelocityId, e.velocity);
                    attribute.SetVector3(ColorId, new Vector3(e.color.x, e.color.y, e.color.z)); attribute.SetFloat(AlphaId, e.color.w);
                    attribute.SetFloat(SizeId, e.size); effect.SendEvent(PlayEventId, attribute); stats.PooledPlayed++;
                }
            }
        }
        public void ShiftAnchors(float3 delta)
        {
            // TODO(VFX-013): origin-shift adjustment and particle anchor lifetime reset.
        }
        public void ClearAll(bool exceptWarp)
        {
            if (resources == null) return;
            for (int i = 1; i < resources.Length; i++)
            {
                if (exceptWarp && Slots[i].Kind == FxKind.Warp) continue;
                var r = resources[i]; if (r?.Effects != null) foreach (var effect in r.Effects) if (effect != null) { effect.Reinit(); effect.Stop(); }
            }
            if (PendingImpacts.IsCreated) PendingImpacts.Clear();
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; Unbind(boundWorld);
            if (resources != null) foreach (var r in resources) r?.Dispose(); resources = null;
            if (Slots.IsCreated) Slots.Dispose(); if (SlotByHash.IsCreated) SlotByHash.Dispose(); if (DefaultSlotByKind.IsCreated) DefaultSlotByKind.Dispose();
            if (Palette.IsCreated) Palette.Dispose(); if (AnchorPositions.IsCreated) AnchorPositions.Dispose(); if (PendingImpacts.IsCreated) PendingImpacts.Dispose();
            Tracers = null; Beams = null; Plumes = null; Shields = null;
        }
        void OnDestroy() => Dispose();
    }
}
