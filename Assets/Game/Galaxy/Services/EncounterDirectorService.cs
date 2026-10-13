using System.Collections.Generic;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Encounters;
using Unity.Entities;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-33 §4.5 director surface; ENC-007 owns spawning, lifecycle and frame callbacks.</summary>
    public sealed class EncounterDirectorService : IGalaxyFrameTick, ISceneExiting, System.IDisposable
    {
        // TODO(ENC-007): compose the canonical owners; never instantiate this shell in boot wiring.
        public EncounterDirectorService(GalaxyService galaxy, StarSystemLoader loader, TravelService travel, DockingService docking,
            IShipSpawner spawner, FactionService factions, TerritoryService territory, World world, ContentCatalog catalog)
            => throw new System.NotImplementedException("ENC-007");
        public int TickOrder => 300;
        public int SceneExitOrder => 95;
        public bool Enabled { get => throw new System.NotImplementedException("ENC-007"); set => throw new System.NotImplementedException("ENC-007"); }
        public EncounterConfigDefinition Config => throw new System.NotImplementedException("ENC-007");
        public int SpawnEncounter(EncounterDefinition def, in EncounterAnchor anchor, int count, EncounterSource source)
            => throw new System.NotImplementedException("ENC-007");
        public int StartCombat(EncounterDefinition def, in EncounterAnchor anchor) => throw new System.NotImplementedException("ENC-007");
        public int ActiveInstanceCount => throw new System.NotImplementedException("ENC-007");
        public int AliveShipCount => throw new System.NotImplementedException("ENC-007");
        public bool TryGetInstance(uint id, out EncounterInstanceInfo info) => throw new System.NotImplementedException("ENC-007");
        public bool TryGetInstanceByMember(ulong persistentId, out EncounterInstanceInfo info) => throw new System.NotImplementedException("ENC-007");
        public void GetActiveInstances(List<EncounterInstanceInfo> into) => throw new System.NotImplementedException("ENC-007");
        public bool IsGloballyUsed(EncounterDefinition def) => throw new System.NotImplementedException("ENC-007");
        public void Tick(in GalaxyFrameContext ctx) => throw new System.NotImplementedException("ENC-007");
        public void OnSceneExiting(in SceneExitContext context) => throw new System.NotImplementedException("ENC-007");
        public void Dispose() => throw new System.NotImplementedException("ENC-007");
    }
}
