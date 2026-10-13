using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Services;
using Game.Galaxy.Generation;
using Game.Data.Definitions;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>Owner of GalaxyDef, star-system content cache and world commands. Implements SPEC-30 §5.8.</summary>
    public sealed class GalaxyService : System.IDisposable, IGalaxyFrameTick
    {
        readonly World world;
        readonly ContentCatalog catalog;
        readonly GalaxyFrameTicker ticker;
        readonly Dictionary<StarSystemId, StarSystemContent> cache = new();
        readonly Dictionary<StarSystemId, PendingStarSystemContent> pending = new();
        readonly Dictionary<StarSystemId, int> contentCounts = new();
        readonly Dictionary<BodyId, string> names = new();
        readonly List<BodyId> releaseNames = new();
        IGalaxyLiveDeltaSink liveSink;
        bool disposed;
        public GalaxyService(Game.Data.Services.ContentCatalog catalog, GalaxyState state, World world, GalaxyFrameTicker ticker)
        {
            if (catalog == null) throw new System.ArgumentNullException(nameof(catalog));
            State = state ?? throw new System.ArgumentNullException(nameof(state));
            this.world = world ?? throw new System.ArgumentNullException(nameof(world));
            if (!world.IsCreated) throw new System.ArgumentException("World must be alive.", nameof(world));
            if (ticker == null) throw new System.ArgumentNullException(nameof(ticker));
            var definitions = catalog.All<GalaxyGenerationDefinition>();
            if (definitions.Count != 1) throw new System.ArgumentException("Exactly one galaxy generation definition is required.", nameof(catalog));
            Definition = definitions[0];
            this.catalog = catalog; this.ticker = ticker; ticker.Register(this);
        }
        public int TickOrder => 0;
        public GalaxyDef Galaxy { get; private set; }                   // null until NewGame/OnSessionRestored
        public GalaxyState State { get; }
        public GalaxyGenerationDefinition Definition { get; }  // the single GalaxyGenerationDefinition in the catalog
        public int GenerationCount { get; private set; }                // galaxy generations since construction (R9 test hook)
        public int ContentGenerationCount(StarSystemId id) => contentCounts.TryGetValue(id, out int count) ? count : 0;  // content generations of `id` since the last NewGame/restore (R9)

        // Lifecycle (called by SPEC-01)
        public void NewGame(ulong seed)
        {
            CheckAlive(); ClearGenerated(); State.Clear();
            ulong selected = Definition.UseFixedSeed ? Definition.FixedSeed : seed;
            if (selected == 0) throw new ArgumentException("Galaxy seed must be nonzero.", nameof(seed));
            State.Seed = selected; State.GeneratorVersion = GalaxyConstants.CurrentGeneratorVersion;
            BuildGalaxy(); State.CurrentStarSystem = Galaxy.StartStarSystem;
            MarkVisited(Galaxy.StartStarSystem); State.HasPlayerLocation = false;
        }                   // seed is ignored when Definition.UseFixedSeed
        public void OnSessionRestored()
        { CheckAlive(); ClearGenerated(); BuildGalaxy(); }
        void BuildGalaxy()
        {
            Galaxy = GalaxyDefBuilder.Build(catalog, State.Seed, State.GeneratorVersion);
            GenerationCount++;
            if (Galaxy.SkippedProceduralCount != 0)
                GameLog.Info("Galaxy", "Skipped " + Galaxy.SkippedProceduralCount + " procedural systems during placement.");
        }                   // after GameSession restored GalaxyState in place
        public PlayerLocation GetStartLocation() => throw new System.NotImplementedException("GAL-008"); // SPEC-30 §6 and SPEC-01 new-game flow

        // Content
        public JobHandle PrefetchContent(StarSystemId id)
        {
            RequireSystem(id);
            if (cache.ContainsKey(id)) return default;
            if (pending.TryGetValue(id, out var scheduled)) return scheduled.Handle;
            scheduled = new PendingStarSystemContent(Galaxy, id);
            try { pending.Add(id, scheduled); }
            catch { scheduled.Dispose(); throw; }
            return scheduled.Handle;
        } // schedules generation; no-op if cached or scheduled
        public StarSystemContent GetContent(StarSystemId id)
        {
            RequireSystem(id);
            if (cache.TryGetValue(id, out var content)) return content;
            bool warning, error;
            if (pending.TryGetValue(id, out var scheduled))
            {
                pending.Remove(id);
                using (scheduled) content = scheduled.Take(out warning, out error);
            }
            else content = StarSystemContentBuilder.Build(Galaxy, id, out warning, out error);
            try
            {
                if (error) throw new GalaxyGenerationException("System content exceeds the validated body capacity: " + id);
                if (warning) GameLog.Warn("Galaxy", "Body pool exhausted while generating " + id + ".");
                if (State.TryGetDelta(id, out var delta)) StarSystemDeltaApplier.Apply(content, delta, Galaxy, catalog);
                cache.Add(id, content); contentCounts[id] = ContentGenerationCount(id) + 1;
                return content;
            }
            catch { content.Dispose(); throw; }
        }   // completes prefetch or generates now; applies deltas; cached
        public void ReleaseContent(StarSystemId id)
        {
            RequireSystem(id);
            if (pending.TryGetValue(id, out var scheduled)) { pending.Remove(id); scheduled.Dispose(); }
            if (cache.TryGetValue(id, out var content)) { cache.Remove(id); content.Dispose(); }
            releaseNames.Clear();
            foreach (var pair in names) if (pair.Key.System.Equals(id)) releaseNames.Add(pair.Key);
            foreach (var key in releaseNames) names.Remove(key);
            releaseNames.Clear();
        }       // disposes the cache entry
        public void SetLiveDeltaSink(IGalaxyLiveDeltaSink sink) { CheckAlive(); liveSink = sink; }
        public uint RequestOriginReset(StarSystemId id, double3 newOrigin) => WriteOriginRequest(id, newOrigin, 0, default);
        public uint RequestOriginReset(StarSystemId id, double3 newOrigin, in PlayerPlacement placement) => WriteOriginRequest(id, newOrigin, 1, placement);
        public uint LastOriginRequestSerial { get; private set; }
        uint WriteOriginRequest(StarSystemId id, double3 newOrigin, byte placePlayer, in PlayerPlacement placement)
        {
            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<FrameOrigin>());
            var entity = query.GetSingletonEntity();
            uint serial = checked(LastOriginRequestSerial + 1);
            em.SetComponentData(entity, new OriginRebaseRequest { StarSystem = id, NewOrigin = newOrigin, Serial = serial, PlacePlayer = placePlayer, Placement = placement });
            em.SetComponentEnabled<OriginRebaseRequest>(entity, true);
            LastOriginRequestSerial = serial;
            return serial;
        }

        // Queries
        public bool TryResolveStarSystem(Game.Core.ContentId authoredId, out StarSystemId id) => throw new System.NotImplementedException("GAL-008");
        public bool TryResolveBody(Game.Core.ContentId authoredStarSystem, string bodyKey, out BodyId id) => throw new System.NotImplementedException("GAL-008");
        public BodyTemplateDefinition GetBodyTemplate(ushort templateIndex) => throw new System.NotImplementedException("GAL-008");
        public string GetStarSystemName(StarSystemId id) => throw new System.NotImplementedException("GAL-008");  // §5.7
        public string GetBodyName(BodyId id) => throw new System.NotImplementedException("GAL-008");              // authored override, else template display name + " " + roman/letter suffix (§5.7)
        public string GetLocationKey(BodyId id) => throw new System.NotImplementedException("GAL-008");           // "<starsys id>/<body key>" for authored bodies, else "<ss:XXXXXXXX>/<role>:<slot>.<subslot>"
        public bool CapturePlayerLocation() => throw new System.NotImplementedException("GAL-008");               // reads PlayerShip UniversePosition + yaw into State; false when no player ship

        // World commands (narrative effects reach these through ILogicContext in Game.App)
        public void ReplaceBody(BodyId body, Game.Core.ContentId template) => throw new System.NotImplementedException("GAL-008");
        public BodyId AddStorySpawn(StarSystemId sys, Game.Core.ContentId template, BodyId anchor, double3 offsetM, float yawRad) => throw new System.NotImplementedException("GAL-008");
        public bool RemoveStorySpawn(BodyId storySpawn) => throw new System.NotImplementedException("GAL-008");
        public bool MarkBodyDestroyed(BodyId body) => throw new System.NotImplementedException("GAL-008");
        public bool MarkBodyInteracted(BodyId body) => throw new System.NotImplementedException("GAL-008");
        public bool IsBodyDestroyed(BodyId body) => throw new System.NotImplementedException("GAL-008");
        public void Reveal(StarSystemId id) { RequireSystem(id); State.AddKnown(id); }               // Known += id (also hidden ones)
        public void MarkVisited(StarSystemId id)
        {
            RequireSystem(id); State.AddVisited(id); State.AddKnown(id);
            for (int slot = 0; slot < Galaxy.LaneCountOf(id); slot++)
            {
                var lane = Galaxy.Lanes[Galaxy.LaneIndexOf(id, slot)];
                var neighbour = GalaxyDef.Neighbour(lane, id);
                if ((lane.Flags & LaneFlags.HiddenUntilKnown) == 0 &&
                    (Galaxy.Get(neighbour).Flags & StarSystemFlags.HiddenUntilKnown) == 0) State.AddKnown(neighbour);
            }
        }          // Visited += id; Known += id and its non-hidden neighbours

        public void Tick(in GalaxyFrameContext ctx) { /* Completed prefetches remain owned until GetContent/ReleaseContent (§5.8 T1). */ }       // completes finished content prefetches (§5.8 step T1); no event draining
        void CheckAlive()
        { if (disposed) throw new ObjectDisposedException(nameof(GalaxyService)); }
        void RequireSystem(StarSystemId id)
        {
            CheckAlive();
            if (Galaxy == null || !Galaxy.TryGetIndex(id, out _)) throw new KeyNotFoundException("Unknown star system: " + id);
        }
        void ClearGenerated()
        {
            // Jobs read Galaxy's arrays: complete/dispose every outstanding output before disposing it.
            foreach (var pair in pending) pair.Value.Dispose();
            pending.Clear();
            foreach (var pair in cache) pair.Value.Dispose();
            cache.Clear(); names.Clear(); releaseNames.Clear(); contentCounts.Clear();
            Galaxy?.Dispose(); Galaxy = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            ticker.Unregister(this); ClearGenerated(); liveSink = null; disposed = true;
        }
        // TODO(GAL-008): queries, names, player placement/capture, delta command patches and required acceptance.
    }
}
