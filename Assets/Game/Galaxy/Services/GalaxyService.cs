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
            // TODO(GAL-008): register the implemented frame tick and initialize generation/cache ownership.
        }
        public int TickOrder => 0;
        public GalaxyDef Galaxy { get; }                   // null until NewGame/OnSessionRestored
        public GalaxyState State { get; }
        public GalaxyGenerationDefinition Definition { get; }  // the single GalaxyGenerationDefinition in the catalog
        public int GenerationCount { get; }                // galaxy generations since construction (R9 test hook)
        public int ContentGenerationCount(StarSystemId id) => throw new System.NotImplementedException("GAL-008");  // content generations of `id` since the last NewGame/restore (R9)

        // Lifecycle (called by SPEC-01)
        public void NewGame(ulong seed) => throw new System.NotImplementedException("GAL-008");                   // seed is ignored when Definition.UseFixedSeed
        public void OnSessionRestored() => throw new System.NotImplementedException("GAL-008");                   // after GameSession restored GalaxyState in place
        public PlayerLocation GetStartLocation() => throw new System.NotImplementedException("GAL-008"); // SPEC-30 §6 and SPEC-01 new-game flow

        // Content
        public JobHandle PrefetchContent(StarSystemId id) => throw new System.NotImplementedException("GAL-008"); // schedules generation; no-op if cached or scheduled
        public StarSystemContent GetContent(StarSystemId id) => throw new System.NotImplementedException("GAL-008");   // completes prefetch or generates now; applies deltas; cached
        public void ReleaseContent(StarSystemId id) => throw new System.NotImplementedException("GAL-008");       // disposes the cache entry
        public void SetLiveDeltaSink(IGalaxyLiveDeltaSink sink) => throw new System.NotImplementedException("GAL-008");
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
        public void Reveal(StarSystemId id) => throw new System.NotImplementedException("GAL-008");               // Known += id (also hidden ones)
        public void MarkVisited(StarSystemId id) => throw new System.NotImplementedException("GAL-008");          // Visited += id; Known += id and its non-hidden neighbours

        public void Tick(in GalaxyFrameContext ctx) => throw new System.NotImplementedException("GAL-008");       // completes finished content prefetches (§5.8 step T1); no event draining
        public void Dispose()
        {
            // TODO(GAL-008): unregister the frame tick and dispose GalaxyDef/cache when those resources exist.
            // Origin state and World belong to ECS; this command surface owns no native allocation.
        }
        // TODO(GAL-008): implement the behavior specified by SPEC-30.
    }
}
