using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Simulation.Commands;
using Unity.Transforms;
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
            EventBus<SessionReset>.Subscribe(OnSessionReset);
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
        public PlayerLocation GetStartLocation()
        {
            CheckAlive();
            if (Galaxy == null) throw new InvalidOperationException("Galaxy is not generated.");
            var start = Definition.StartStarSystem;
            if (!TryResolveBody(start.Id, start.StartBodyKey, out var id)) throw new GalaxyGenerationException("Authored start body is missing.");
            var content = GetContent(id.System);
            if (!content.TryFind(id.LocalKey, out int index)) throw new GalaxyGenerationException("Generated start body is missing.");
            var body = content.Bodies[index];
            using var clocks = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
            double time = clocks.IsEmptyIgnoreFilter ? 0 : clocks.GetSingleton<SimClock>().UniversalTime;
            var offset = math.mul(Planar.YawRotation(body.YawRad), (float3)start.StartOffsetM);
            return new PlayerLocation { StarSystem = id.System, PositionM = OrbitMath.Position(body, time) + (double3)offset,
                YawRad = math.radians(start.StartYawDegrees) };
        } // SPEC-30 §6 and SPEC-01 new-game flow

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
        public bool TryResolveStarSystem(ContentId authoredId, out StarSystemId id)
        {
            id = default;
            if (Galaxy == null || !catalog.TryGet<AuthoredStarSystemDefinition>(authoredId, out var definition)) return false;
            var candidate = StarSystemId.FromAuthored(definition.Id.Hash);
            if (!Galaxy.TryGetIndex(candidate, out _)) return false;
            id = candidate; return true;
        }
        public bool TryResolveBody(ContentId authoredStarSystem, string bodyKey, out BodyId id)
        {
            id = default;
            if (!TryResolveStarSystem(authoredStarSystem, out var system)) return false;
            var definition = Galaxy.Authored[Galaxy.Get(system).AuthoredIndex - 1];
            for (int i = 0; i < definition.Bodies.Count; i++)
                if (string.Equals(definition.Bodies[i].key, bodyKey, StringComparison.Ordinal))
                { id = AuthoredId(system, definition, i); return true; }
            return false;
        }
        static BodyId AuthoredId(StarSystemId system, AuthoredStarSystemDefinition definition, int index)
        {
            var body = definition.Bodies[index];
            if (body.role != BodyRole.Moon) return new BodyId(system, body.role, body.slot, 0);
            ushort parentSlot = 0, sub = 0;
            foreach (var candidate in definition.Bodies)
                if (candidate.key == body.parentKey) { parentSlot = candidate.slot; break; }
            for (int i = 0; i < index; i++)
                if (definition.Bodies[i].role == BodyRole.Moon && definition.Bodies[i].parentKey == body.parentKey) sub++;
            return new BodyId(system, BodyRole.Moon, parentSlot, sub);
        }
        AuthoredBody FindAuthoredBody(BodyId id, out AuthoredStarSystemDefinition definition)
        {
            definition = null;
            var system = Galaxy.Get(id.System);
            if (system.AuthoredIndex == 0) return null;
            definition = Galaxy.Authored[system.AuthoredIndex - 1];
            for (int i = 0; i < definition.Bodies.Count; i++)
                if (AuthoredId(id.System, definition, i).Equals(id)) return definition.Bodies[i];
            return null;
        }
        public BodyTemplateDefinition GetBodyTemplate(ushort templateIndex)
        {
            CheckAlive();
            if (Galaxy == null || templateIndex >= Galaxy.BodyTemplates.Count) throw new ArgumentOutOfRangeException(nameof(templateIndex));
            return Galaxy.BodyTemplates[templateIndex];
        }
        public string GetStarSystemName(StarSystemId id) { RequireSystem(id); return GalaxyNames.System(Galaxy, id); }
        public string GetBodyName(BodyId id)
        {
            RequireSystem(id.System);
            if (names.TryGetValue(id, out string name)) return name;
            var content = GetContent(id.System);
            if (!content.TryFind(id.LocalKey, out int index)) throw new KeyNotFoundException("Unknown body: " + id);
            var authored = FindAuthoredBody(id, out _);
            var body = content.Bodies[index];
            if (authored != null && !string.IsNullOrEmpty(authored.displayNameOverride)) name = authored.displayNameOverride;
            else name = id.Role switch
            {
                BodyRole.Star => GetStarSystemName(id.System),
                BodyRole.Planet => GetStarSystemName(id.System) + " " + GalaxyNames.Roman(id.Slot + 1),
                BodyRole.Moon => GetBodyName(BodyId.FromLocalKey(id.System, body.ParentKey)) + " " + (char)('a' + id.SubSlot),
                BodyRole.LaneGate => "Gate to " + GetStarSystemName(GalaxyDef.Neighbour(Galaxy.Lanes[body.LaneIndex], id.System)),
                _ => GetBodyTemplate(body.TemplateIndex).DisplayName
            };
            names.Add(id, name); return name;
        }
        public string GetLocationKey(BodyId id)
        {
            RequireSystem(id.System);
            var body = FindAuthoredBody(id, out var definition);
            return body != null ? definition.IdString + "/" + body.key : id.ToString();
        }
        public bool CapturePlayerLocation()
        {
            CheckAlive();
            using var players = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerShip>(),
                ComponentType.ReadOnly<UniversePosition>(), ComponentType.ReadOnly<LocalTransform>());
            if (players.IsEmptyIgnoreFilter) return false;
            var pose = players.GetSingleton<LocalTransform>();
            State.PlayerLocation = new PlayerLocation { StarSystem = State.CurrentStarSystem,
                PositionM = players.GetSingleton<UniversePosition>().Value,
                YawRad = Planar.YawFromForward(math.mul(pose.Rotation, new float3(0, 0, 1))) };
            State.HasPlayerLocation = true; return true;
        }

        // World commands (narrative effects reach these through ILogicContext in Game.App)
        public void ReplaceBody(BodyId body, ContentId template)
        {
            RequireSystem(body.System);
            if (!ResolveCommandTemplate(template, out _, out ushort index)) return;
            State.GetOrCreateDelta(body.System).SetReplacement(body.LocalKey, template);
            if (cache.TryGetValue(body.System, out var content) && content.TryFind(body.LocalKey, out int bodyIndex))
            {
                var changed = content.Bodies[bodyIndex]; changed.TemplateIndex = index; changed.State |= BodyStateFlags.Replaced;
                content.Bodies[bodyIndex] = changed;
            }
            names.Remove(body); liveSink?.OnBodyDeltaChanged(body);
        }
        public BodyId AddStorySpawn(StarSystemId system, ContentId template, BodyId anchor, double3 offsetM, float yawRad)
        {
            RequireSystem(system);
            if (anchor.IsValid && !anchor.System.Equals(system)) throw new ArgumentException("Story anchor belongs to another system.", nameof(anchor));
            if (!ResolveCommandTemplate(template, out var definition, out ushort index)) return default;
            var record = new StorySpawnRecord { Template = template, AnchorBodyKey = anchor.IsValid ? anchor.LocalKey : 0, OffsetM = offsetM, YawRad = yawRad };
            int slot = State.GetOrCreateDelta(system).AddStorySpawn(record);
            var id = new BodyId(system, BodyRole.StorySpawn, checked((ushort)slot), 0);
            if (cache.TryGetValue(system, out var content)) StarSystemDeltaApplier.AppendStory(content, record, slot, Galaxy, definition, index);
            liveSink?.OnBodyDeltaChanged(id); return id;
        }
        public bool RemoveStorySpawn(BodyId body)
        {
            RequireSystem(body.System);
            if (body.Role != BodyRole.StorySpawn || body.SubSlot != 0 || !State.TryGetDelta(body.System, out var delta) || !delta.RemoveStorySpawn(body.Slot)) return false;
            if (cache.TryGetValue(body.System, out var content) && content.TryFind(body.LocalKey, out int index)) content.Bodies.RemoveAt(index);
            names.Remove(body); liveSink?.OnBodyDeltaChanged(body); return true;
        }
        /// <summary>ADR-0077: remove a body and its saved hulk, retaining the existing persistent tombstone.</summary>
        public void RemoveBody(BodyId body)
        {
            RequireSystem(body.System);
            if (!body.IsValid) throw new ArgumentException("Body identity is required.", nameof(body));
            liveSink?.PrepareBodyRemoval(body);
            var delta = State.GetOrCreateDelta(body.System);
            delta.RemoveStructure(body.LocalKey);
            delta.MarkDestroyed(body.LocalKey);
            if (body.Role == BodyRole.StorySpawn) delta.RemoveStorySpawn(body.Slot);
            if (cache.TryGetValue(body.System, out var content) && content.TryFind(body.LocalKey, out int index))
            {
                var changed = content.Bodies[index];
                changed.State = (changed.State | BodyStateFlags.Destroyed) & ~BodyStateFlags.HasStructure;
                content.Bodies[index] = changed;
            }
            names.Remove(body); liveSink?.OnBodyDeltaChanged(body);
        }
        public bool MarkBodyDestroyed(BodyId body)
        {
            RequireSystem(body.System);
            if (!State.GetOrCreateDelta(body.System).MarkDestroyed(body.LocalKey)) return false;
            PatchFlag(body, BodyStateFlags.Destroyed); return true;
        }
        public bool MarkBodyInteracted(BodyId body)
        {
            RequireSystem(body.System);
            if (!State.GetOrCreateDelta(body.System).MarkInteracted(body.LocalKey)) return false;
            PatchFlag(body, BodyStateFlags.Interacted); return true;
        }
        void PatchFlag(BodyId body, BodyStateFlags flag)
        {
            if (cache.TryGetValue(body.System, out var content) && content.TryFind(body.LocalKey, out int index))
            { var changed = content.Bodies[index]; changed.State |= flag; content.Bodies[index] = changed; }
            liveSink?.OnBodyDeltaChanged(body);
        }
        bool ResolveCommandTemplate(ContentId id, out BodyTemplateDefinition template, out ushort index)
        {
            index = GalaxyConstants.NoIndex;
            if (catalog.TryGet(id, out template)) index = Galaxy.BodyTemplateIndexOf(template);
            if (index != GalaxyConstants.NoIndex) return true;
            GameLog.Warn("Galaxy", "Unknown body template: " + id); return false;
        }
        public bool IsBodyDestroyed(BodyId body)
        {
            if (!State.TryGetDelta(body.System, out var delta)) return false;
            int lo = 0, hi = delta.Destroyed.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2; ulong key = delta.Destroyed[mid];
                if (key == body.LocalKey) return true;
                if (key < body.LocalKey) lo = mid + 1; else hi = mid - 1;
            }
            return false;
        }
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
        void OnSessionReset(SessionReset reset)
        {
            // Events are deferred: an old Cleared event must not discard a newer campaign.
            if (State.Seed == 0 && (reset.Reason == SessionResetReason.Cleared ||
                reset.Reason == SessionResetReason.NewGame)) ClearGenerated();
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
            EventBus<SessionReset>.Unsubscribe(OnSessionReset); ticker.Unregister(this); ClearGenerated(); liveSink = null; disposed = true;
        }
        // Streamed scene bodies and PLOC restoration remain owned by SPEC-31.
    }
}
