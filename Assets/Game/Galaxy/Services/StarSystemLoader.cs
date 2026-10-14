using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Components;
using Game.Simulation.Commands;
using Unity.Transforms;
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
        readonly DockingService docking;
        readonly World world;
        readonly VoxelStructureRegistry registry;
        readonly StarSystemSpawnHook hook = new();
        readonly Dictionary<ulong, LoadedBody> bodies = new();
        readonly Dictionary<float, BlobAssetReference<Collider>> colliders = new();
        readonly EntityQuery members, chunks, splits, origins, info, worldActive, disposal;
        readonly EntityQuery players, streaming, blockDefs, orientations, bodyEvents;
        readonly BlueprintLibrary blueprints;
        readonly IPersistentIdAllocator ids;
        readonly ContentCatalog catalog;
        readonly List<T2Streamer> retiring = new();
        T2Streamer t2;
        BeltStreamer t3;
        readonly List<BeltStreamer> retiringBelts = new();
        bool applyingBodyState;
        readonly Entity infoEntity;
        readonly bool ownsInfo;
        StarSystemId target;
        PlayerLocation arrival;
        StarSystemEntryReason entryReason;
        uint originSerial;
        bool disposed, anchorsDirty;
        public StarSystemLoader(GalaxyService galaxy, World world, VoxelStructureRegistry registry,
            BlueprintLibrary blueprints, IPersistentIdAllocator ids, ContentCatalog catalog, DockingService docking = null)
        {
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.docking = docking ?? new DockingService();
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.registry = registry != null ? registry : throw new ArgumentNullException(nameof(registry));
            if (blueprints == null || ids == null || catalog == null) throw new ArgumentNullException("Loader dependencies");
            if (!world.IsCreated) throw new ArgumentException("World must be alive", nameof(world));
            this.blueprints = blueprints; this.ids = ids; this.catalog = catalog;
            var em = world.EntityManager;
            players = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<LocalTransform>());
            streaming = em.CreateEntityQuery(ComponentType.ReadOnly<StreamingConfig>());
            blockDefs = em.CreateEntityQuery(ComponentType.ReadOnly<BlockDefsRef>());
            orientations = em.CreateEntityQuery(ComponentType.ReadOnly<OrientationTablesRef>());
            bodyEvents = em.CreateEntityQuery(ComponentType.ReadWrite<BodyGridEventQueue>());
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
            EventBus<BodyInteracted>.Subscribe(OnBodyInteracted);
            EventBus<SceneTransitionFinished>.Subscribe(OnTransitionFinished);
            EventBus<SessionReset>.Subscribe(OnSessionReset);
        }
        public int TickOrder => 100;
        public int SceneReadyOrder => SceneHookOrder.Galaxy;
        public int SceneExitOrder => SceneHookOrder.Galaxy;
        public LoaderState State { get; private set; }
        public StarSystemId Loaded { get; private set; }
        public bool IsReady => State == LoaderState.Ready;
        public bool BeginStarSystemChange(StarSystemId target, double3 arrivalPositionM, float arrivalYawRad, StarSystemEntryReason reason)
        {
            CheckAlive();
            if (!IsReady || galaxy.Galaxy == null || !galaxy.Galaxy.TryGetIndex(target, out _) ||
                !math.all(math.isfinite(arrivalPositionM)) || !math.isfinite(arrivalYawRad)) return false;
            var location = new PlayerLocation { StarSystem = target, PositionM = arrivalPositionM, YawRad = arrivalYawRad };
            // Start the existing owner's prefetch before retiring the current content.
            galaxy.PrefetchContent(target);
            if (docking.HasPendingRestore) docking.RestoreDocked(default);
            StartLoad(target, location, reason, true);
            galaxy.State.PlayerLocation = location; galaxy.State.HasPlayerLocation = true;
            info.SetSingleton(new CurrentStarSystemInfo { Id = target, Changing = 1 });
            return true;
        }
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
                if ((spec.State & BodyStateFlags.Destroyed) != 0 && !galaxy.State.GetOrCreateDelta(Loaded).TryGetStructure(spec.Id.LocalKey, out _)) continue;
                var template = galaxy.GetBodyTemplate(spec.TemplateIndex);
                if (template.StructureSource == StructureSource.None) continue;
                double radius = spec.RadiusM + template.T2LoadDistanceM;
                if (math.distancesq(positionM, OrbitMath.Position(spec, time)) >= radius * radius) continue;
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
        void StartLoad(StarSystemId id, PlayerLocation location, StarSystemEntryReason reason, bool placePlayer = false)
        {
            Unload(); target = id; arrival = location; entryReason = reason;
            originSerial = placePlayer
                ? galaxy.RequestOriginReset(id, location.PositionM, new PlayerPlacement { PositionM = location.PositionM, YawRad = location.YawRad })
                : galaxy.RequestOriginReset(id, location.PositionM);
            State = LoaderState.Loading;
        }
        public void Tick(in GalaxyFrameContext ctx)
        {
            CheckAlive(); DrainRetired(); if (worldActive.IsEmptyIgnoreFilter) return;
            DrainBodyEvents();
            if (State == LoaderState.Loading)
            {
                if (origins.IsEmptyIgnoreFilter) return;
                var origin = origins.GetSingleton<FrameOrigin>();
                if (!origin.StarSystem.Equals(target) || origin.AppliedRequestSerial < originSerial) return;
                Loaded = target;
                try
                {
                    var content = galaxy.GetContent(Loaded);
                    AnchorBuilder.Create(world, galaxy.Galaxy, content, galaxy.State.GetOrCreateDelta(content.Id), ctx.UniversalTime, origin.OriginInStarSystem, bodies, colliders);
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
                TickStreaming(ctx.UniversalTime);
            }
        }
        public bool IsSceneReadyComplete(in SceneReadyContext context)
            => context.Scene != GameScene.World || IsArrivalReady;
        public void OnSceneExiting(in SceneExitContext context) { if (context.Scene == GameScene.World) Unload(); }
        public void PrepareBodyRemoval(BodyId body)
        {
            CheckAlive(); world.EntityManager.CompleteAllTrackedJobs();
            t2?.DiscardBody(body);
            foreach (var old in retiring) old.DiscardBody(body);
        }
        public void OnBodyDeltaChanged(BodyId body)
        { CheckAlive(); if (!applyingBodyState && body.System.Equals(Loaded)) anchorsDirty = true; }
        void RefreshAnchors(double time)
        {
            FlushStructures(); RetireStreaming(); var em = world.EntityManager; em.CompleteAllTrackedJobs();
            foreach (var body in bodies.Values) if (em.Exists(body.Anchor)) em.DestroyEntity(body.Anchor);
            bodies.Clear(); RetireColliders();
            AnchorBuilder.Create(world, galaxy.Galaxy, galaxy.GetContent(Loaded), galaxy.State.GetOrCreateDelta(Loaded), time,
                origins.GetSingleton<FrameOrigin>().OriginInStarSystem, bodies, colliders);
            info.SetSingleton(new CurrentStarSystemInfo { Id = Loaded, Loaded = 1, BodyCount = checked((ushort)bodies.Count) });
            anchorsDirty = false;
            if (docking.IsDocked && !docking.HasPendingRestore && !IsRestoredDockAvailable(docking.DockedBody)) docking.Undock();
        }
        public void FlushStructures()
        {
            if (!world.IsCreated || worldActive.IsEmptyIgnoreFilter) return;
            world.EntityManager.CompleteAllTrackedJobs();
            t2?.Flush(); foreach (var old in retiring) old.Flush();
        }
        void OnSessionReset(SessionReset value)
        {
            if (value.Reason != SessionResetReason.Loaded) docking.RestoreDocked(default);
        }
        void OnTransitionFinished(SceneTransitionFinished value)
        {
            if (value.Success && value.Scene == GameScene.World && !disposed)
                docking.CompleteRestore(IsRestoredDockAvailable);
        }
        bool IsRestoredDockAvailable(BodyId body)
        {
            if (disposed || !IsReady || !body.System.Equals(Loaded) || !world.IsCreated ||
                !bodies.TryGetValue(body.LocalKey, out var loaded)) return false;
            var em = world.EntityManager;
            if (!em.Exists(loaded.Anchor)) return false;
            var target = em.Exists(loaded.Grid) ? loaded.Grid : loaded.Anchor;
            return !em.HasComponent<Game.Voxel.Damage.Hulk>(target) &&
                em.HasComponent<Game.Galaxy.Interaction.Interactable>(target) &&
                em.GetComponentData<Game.Galaxy.Interaction.Interactable>(target).Kind == InteractionKind.Dock;
        }
        void OnBodyInteracted(BodyInteracted value)
        {
            if (disposed || !IsReady || value.StarSystemValue != Loaded.Value ||
                !bodies.TryGetValue(value.BodyKey, out var loaded)) return;
            var body = BodyId.FromLocalKey(Loaded, value.BodyKey);
            if (!body.IsValid || !world.IsCreated || !world.EntityManager.Exists(loaded.Anchor)) return;
            // Interacted is persistent metadata, not a request to retire and recreate live geometry.
            applyingBodyState = true;
            try { galaxy.MarkBodyInteracted(body); }
            finally { applyingBodyState = false; }
            if ((InteractionKind)value.Kind == InteractionKind.Dock) docking.Dock(body);
        }
        void Unload()
        {
            if (!docking.HasPendingRestore) docking.Undock();
            if (!world.IsCreated) return;
            State = LoaderState.Unloading; var em = world.EntityManager;
            FlushStructures(); RetireStreaming(); em.CompleteAllTrackedJobs();
            if (Loaded.IsValid)
            {
                // Adopt last-frame pieces before destroying the system's members.
                SplitAdoption.Apply(em, splits, Loaded);
                members.SetSharedComponentFilter(new StarSystemMember { Id = Loaded });
                try { em.DestroyEntity(members); } finally { members.ResetFilter(); }
                em.DestroyEntity(chunks);
            }
            registry.ReconcileAbandonedReads(em);
            foreach (var belt in retiringBelts) belt.CompleteJobs();
            DrainRetired();
            RetireColliders();
            if (Loaded.IsValid && galaxy.Galaxy != null) galaxy.ReleaseContent(Loaded);
            bodies.Clear(); Loaded = default; target = default; anchorsDirty = false; State = LoaderState.Idle;
            if (em.Exists(infoEntity)) info.SetSingleton(default(CurrentStarSystemInfo));
        }
        void TickStreaming(double time)
        {
            if (retiring.Count != 0 || retiringBelts.Count != 0 || streaming.IsEmptyIgnoreFilter || blockDefs.IsEmptyIgnoreFilter || orientations.IsEmptyIgnoreFilter) return;
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            var content = new VoxelContent { Blocks = blockDefs.GetSingleton<BlockDefsRef>().Value,
                Orientations = orientations.GetSingleton<OrientationTablesRef>().Value };
            if (t2 == null)
            {
                t2 = new T2Streamer(em, galaxy, new BodyGridSpawner(em, registry, blueprints, catalog, content, ids),
                    galaxy.State.GetOrCreateDelta(Loaded), bodies);
            }
            // Initial entry has no player until scene-ready completes. Stream at the actual requested arrival first.
            double3 origin = origins.GetSingleton<FrameOrigin>().OriginInStarSystem;
            double3 position = players.IsEmptyIgnoreFilter ? arrival.PositionM : origin + (double3)players.GetSingleton<LocalTransform>().Position;
            var config = streaming.GetSingleton<StreamingConfig>();
            var systemContent = galaxy.GetContent(Loaded);
            int budget = config.MaxGridSpawnsPerFrame - t2.Tick(systemContent, position, origin, time, config);
            if (players.IsEmptyIgnoreFilter) return; // T3 requires a real player; arrival readiness belongs to T2.
            t3 ??= new BeltStreamer(galaxy, galaxy.State.GetOrCreateDelta(Loaded),
                new AsteroidBuilder(catalog, content), new AsteroidGridSpawner(em, registry, ids, bodies));
            t3.Tick(systemContent, position, origin, config, ref budget);
        }
        void DrainBodyEvents()
        {
            if (bodyEvents.IsEmptyIgnoreFilter) return;
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            var queue = bodyEvents.GetSingleton<BodyGridEventQueue>().Events;
            while (queue.TryDequeue(out var change))
            {
                if (!change.Body.System.Equals(Loaded) || !bodies.TryGetValue(change.Body.LocalKey, out var body) ||
                    body.Grid != change.Grid || !em.Exists(body.Anchor)) continue;
                t2?.Flush();
                bool changed;
                applyingBodyState = true;
                try { changed = galaxy.MarkBodyDestroyed(change.Body); }
                finally { applyingBodyState = false; }
                var anchor = em.GetComponentData<StarSystemBody>(body.Anchor);
                anchor.Flags |= BodyFlags.Destroyed; em.SetComponentData(body.Anchor, anchor);
                if (changed) EventBus<BodyDestroyed>.Publish(new BodyDestroyed(change.Body.System.Value, change.Body.LocalKey));
                if (change.Kind == 1 && em.Exists(change.Grid) && em.HasComponent<GridDespawnRequest>(change.Grid))
                    GridSpawnApi.RequestDespawn(em, change.Grid);
            }
        }
        void RetireStreaming()
        {
            if (t2 != null) { t2.BeginRetirement(); retiring.Add(t2); t2 = null; }
            if (t3 != null) { t3.BeginRetirement(); retiringBelts.Add(t3); t3 = null; }
        }
        void DrainRetired()
        {
            for (int i = retiring.Count - 1; i >= 0; i--)
                if (retiring[i].DrainRetired()) { retiring[i].Dispose(); retiring.RemoveAt(i); }
            for (int i = retiringBelts.Count - 1; i >= 0; i--)
                if (retiringBelts[i].DrainRetired()) { retiringBelts[i].Dispose(); retiringBelts.RemoveAt(i); }
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
            docking.RestoreDocked(default);
            Unload(); DrainRetired();
            if (retiring.Count != 0 || retiringBelts.Count != 0) throw new InvalidOperationException("Loader disposal requires completed grid/read retirement.");
            disposed = true; EventBus<BodyInteracted>.Unsubscribe(OnBodyInteracted);
            EventBus<SessionReset>.Unsubscribe(OnSessionReset);
            EventBus<SceneTransitionFinished>.Unsubscribe(OnTransitionFinished); registry.RemoveSpawnHook(hook); galaxy.SetLiveDeltaSink(null);
            if (!world.IsCreated) return;
            if (ownsInfo && world.EntityManager.Exists(infoEntity)) world.EntityManager.DestroyEntity(infoEntity);
            players.Dispose(); streaming.Dispose(); blockDefs.Dispose(); orientations.Dispose(); bodyEvents.Dispose();
            members.Dispose(); chunks.Dispose(); splits.Dispose(); origins.Dispose(); info.Dispose(); worldActive.Dispose(); disposal.Dispose();
        }
    }
}
