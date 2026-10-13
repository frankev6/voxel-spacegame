using Game.Data.Definitions;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>Owner of GalaxyDef, star-system content cache and world commands. Implements SPEC-30 §5.8.</summary>
    public sealed class GalaxyService : System.IDisposable, IGalaxyFrameTick
    {
        public GalaxyService(Game.Data.Services.ContentCatalog catalog, GalaxyState state, Unity.Entities.World world, GalaxyFrameTicker ticker) => throw new System.NotImplementedException("GAL-008");
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
        public uint RequestOriginReset(StarSystemId id, double3 newOrigin) => throw new System.NotImplementedException("GAL-008"); // enables OriginRebaseRequest with Serial+1; returns the serial
        public uint RequestOriginReset(StarSystemId id, double3 newOrigin, in PlayerPlacement placement) => throw new System.NotImplementedException("GAL-008"); // same, and places the player ship
        public uint LastOriginRequestSerial { get; }

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
        public void Dispose() => throw new System.NotImplementedException("GAL-008");                             // disposes GalaxyDef and cached content
        // TODO(GAL-008): implement the behavior specified by SPEC-30.
    }
}
