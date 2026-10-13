using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Streaming;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;
using Collider = Unity.Physics.Collider;

namespace Game.Galaxy.Services
{
    /// <summary>Owns star-system T1 lifetime; SYS-006/007 extend its voxel/belt streaming.</summary>
    public sealed class StarSystemLoader : IGalaxyFrameTick, IGalaxyLiveDeltaSink, ISceneReady, ISceneExiting, IDisposable
    {
        readonly GalaxyService galaxy;
        readonly World world;
        readonly VoxelStructureRegistry registry;
        readonly StarSystemSpawnHook hook = new();
        readonly Dictionary<ulong, LoadedBody> bodies = new();
        readonly Dictionary<float, BlobAssetReference<Collider>> colliders = new();
        readonly EntityQuery members, chunks, splits, origins, info, worldActive, disposal;
        readonly Entity infoEntity;
        readonly bool ownsInfo;
        StarSystemId target;
        PlayerLocation arrival;
        StarSystemEntryReason entryReason;
        uint originSerial;
        bool disposed, anchorsDirty;
        public StarSystemLoader(GalaxyService galaxy, World world, VoxelStructureRegistry registry,
            BlueprintLibrary blueprints, IPersistentIdAllocator ids, ContentCatalog catalog)
        {
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.registry = registry != null ? registry : throw new ArgumentNullException(nameof(registry));
            if (blueprints == null || ids == null || catalog == null) throw new ArgumentNullException("Loader dependencies");
            if (!world.IsCreated) throw new ArgumentException("World must be alive", nameof(world));
            var em = world.EntityManager;
            members = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<StarSystemMember>() }, Options = EntityQueryOptions.IncludeDisabledEntities });
            chunks = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<ChunkBody>() }, Options = EntityQueryOptions.IncludeDisabledEntities });
            splits = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<SplitOrigin>() }, Options = EntityQueryOptions.IncludeDisabledEntities });
            origins = em.CreateEntityQuery(ComponentType.ReadOnly<FrameOrigin>());
            worldActive = em.CreateEntityQuery(ComponentType.ReadOnly<WorldSceneActive>());
            disposal = em.CreateEntityQuery(ComponentType.ReadWrite<DeferredDisposalQueue>());
            info = em.CreateEntityQuery(ComponentType.ReadWrite<CurrentStarSystemInfo>());
            ownsInfo = info.IsEmptyIgnoreFilter;
            infoEntity = ownsInfo ? em.CreateEntity(typeof(CurrentStarSystemInfo)) : info.GetSingletonEntity();
            registry.AddSpawnHook(hook); galaxy.SetLiveDeltaSink(this);
        }
        public int TickOrder => 100;
        public int SceneReadyOrder => SceneHookOrder.Galaxy;
        public int SceneExitOrder => SceneHookOrder.Galaxy;
        public LoaderState State { get; private set; }
        public StarSystemId Loaded { get; private set; }
        public bool IsReady => State == LoaderState.Ready;
        public bool BeginStarSystemChange(StarSystemId target, double3 arrivalPositionM, float arrivalYawRad, StarSystemEntryReason reason)
            => throw new NotImplementedException("SYS-013");
        public bool IsArrivalReady => IsReady && IsAreaReady(arrival.PositionM);
        public bool IsAreaReady(double3 positionM)
        {
            if (!IsReady) return false;
            if (!Loaded.IsValid) return true; // Actual sandbox: no generated galaxy or bodies.
            var content = galaxy.GetContent(Loaded); var em = world.EntityManager;
            using var clocks = em.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
            double time = clocks.IsEmptyIgnoreFilter ? 0 : clocks.GetSingleton<SimClock>().UniversalTime;
            foreach (var spec in content.Bodies)
            {
                if ((spec.State & BodyStateFlags.Destroyed) != 0) continue;
                var template = galaxy.GetBodyTemplate(spec.TemplateIndex);
                if (template.StructureSource == StructureSource.None) continue;
                double radius = spec.RadiusM + template.T2LoadDistanceM;
                if (math.distancesq(positionM, OrbitMath.Position(spec, time)) > radius * radius) continue;
                if (!bodies.TryGetValue(spec.Id.LocalKey, out var body) || !em.Exists(body.Grid) ||
                    !em.HasComponent<GridSpawning>(body.Grid) || em.IsComponentEnabled<GridSpawning>(body.Grid)) return false;
            }
            return true;
        }
        public void OnSceneReady(in SceneReadyContext context)
        {
            CheckAlive(); if (context.Scene != GameScene.World) return;
            if (galaxy.Galaxy == null) { State = LoaderState.Ready; return; }
            var id = galaxy.State.CurrentStarSystem;
            if (!galaxy.Galaxy.TryGetIndex(id, out _))
            {
                GameLog.Error("Galaxy", "Saved star system is unavailable; returning to the start system.");
                id = galaxy.Galaxy.StartStarSystem; galaxy.State.HasPlayerLocation = false;
            }
            if (!galaxy.State.HasPlayerLocation)
            { galaxy.State.PlayerLocation = galaxy.GetStartLocation(); galaxy.State.HasPlayerLocation = true; }
            var reason = context.Reason == SceneEntryReason.LoadGame ? StarSystemEntryReason.Load :
                context.Reason == SceneEntryReason.ExitBlueprintLab ? StarSystemEntryReason.ReturnFromEditor : StarSystemEntryReason.NewGame;
            StartLoad(id, galaxy.State.PlayerLocation, reason);
        }
        void StartLoad(StarSystemId id, PlayerLocation location, StarSystemEntryReason reason)
        {
            Unload(); target = id; arrival = location; entryReason = reason;
            originSerial = galaxy.RequestOriginReset(id, location.PositionM); State = LoaderState.Loading;
        }
        public void Tick(in GalaxyFrameContext ctx)
        {
            CheckAlive(); if (worldActive.IsEmptyIgnoreFilter) return;
            if (State == LoaderState.Loading)
            {
                if (origins.IsEmptyIgnoreFilter) return;
                var origin = origins.GetSingleton<FrameOrigin>();
                if (!origin.StarSystem.Equals(target) || origin.AppliedRequestSerial < originSerial) return;
                Loaded = target;
                try
                {
                    var content = galaxy.GetContent(Loaded);
                    AnchorBuilder.Create(world, galaxy.Galaxy, content, ctx.UniversalTime, origin.OriginInStarSystem, bodies, colliders);
                    var delta = galaxy.State.GetOrCreateDelta(Loaded); delta.IncrementVisit(); galaxy.MarkVisited(Loaded);
                    galaxy.State.CurrentStarSystem = Loaded;
                    info.SetSingleton(new CurrentStarSystemInfo { Id = Loaded, Loaded = 1, BodyCount = checked((ushort)bodies.Count) });
                    State = LoaderState.Ready;
                    EventBus<StarSystemEntered>.Publish(new StarSystemEntered(Loaded.Value, entryReason, delta.VisitCount));
                }
                catch { Unload(); throw; }
            }
            if (State == LoaderState.Ready && Loaded.IsValid)
            {
                if (anchorsDirty) RefreshAnchors(ctx.UniversalTime);
                SplitAdoption.Apply(world.EntityManager, splits, Loaded);
            }
            // TODO(SYS-006/SYS-007): T2 body grids and T3 asteroid streaming, including arrival readiness.
        }
        public bool IsSceneReadyComplete(in SceneReadyContext context)
            => context.Scene != GameScene.World || IsArrivalReady;
        public void OnSceneExiting(in SceneExitContext context) { if (context.Scene == GameScene.World) Unload(); }
        public void OnBodyDeltaChanged(BodyId body)
        { CheckAlive(); if (body.System.Equals(Loaded)) anchorsDirty = true; }
        void RefreshAnchors(double time)
        {
            FlushStructures(); var em = world.EntityManager; em.CompleteAllTrackedJobs();
            foreach (var body in bodies.Values) if (em.Exists(body.Anchor)) em.DestroyEntity(body.Anchor);
            bodies.Clear(); RetireColliders();
            AnchorBuilder.Create(world, galaxy.Galaxy, galaxy.GetContent(Loaded), time,
                origins.GetSingleton<FrameOrigin>().OriginInStarSystem, bodies, colliders);
            info.SetSingleton(new CurrentStarSystemInfo { Id = Loaded, Loaded = 1, BodyCount = checked((ushort)bodies.Count) });
            anchorsDirty = false;
        }
        public void FlushStructures()
        {
            foreach (var body in bodies.Values)
                if (body.Grid != Entity.Null) throw new NotImplementedException("SYS-006 structure writeback");
        }
        void Unload()
        {
            if (!world.IsCreated) return;
            State = LoaderState.Unloading; var em = world.EntityManager;
            FlushStructures(); em.CompleteAllTrackedJobs();
            if (Loaded.IsValid)
            {
                // Adopt last-frame pieces before destroying the system's members.
                SplitAdoption.Apply(em, splits, Loaded);
                members.SetSharedComponentFilter(new StarSystemMember { Id = Loaded });
                try { em.DestroyEntity(members); } finally { members.ResetFilter(); }
                em.DestroyEntity(chunks);
            }
            RetireColliders();
            if (Loaded.IsValid && galaxy.Galaxy != null) galaxy.ReleaseContent(Loaded);
            bodies.Clear(); Loaded = default; target = default; anchorsDirty = false; State = LoaderState.Idle;
            if (em.Exists(infoEntity)) info.SetSingleton(default(CurrentStarSystemInfo));
        }
        void RetireColliders()
        {
            if (colliders.Count != 0)
            {
                if (disposal.IsEmptyIgnoreFilter) throw new InvalidOperationException("Voxel deferred disposal queue is required for celestial colliders");
                ref var queue = ref disposal.GetSingletonRW<DeferredDisposalQueue>().ValueRW;
                foreach (var blob in colliders.Values) DeferredDisposal.RetireBlob(ref queue, blob);
                colliders.Clear();
            }
        }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(StarSystemLoader)); }
        public void Dispose()
        {
            if (disposed) return;
            Unload(); disposed = true; registry.RemoveSpawnHook(hook); galaxy.SetLiveDeltaSink(null);
            if (!world.IsCreated) return;
            if (ownsInfo && world.EntityManager.Exists(infoEntity)) world.EntityManager.DestroyEntity(infoEntity);
            members.Dispose(); chunks.Dispose(); splits.Dispose(); origins.Dispose(); info.Dispose(); worldActive.Dispose(); disposal.Dispose();
        }
    }
}
