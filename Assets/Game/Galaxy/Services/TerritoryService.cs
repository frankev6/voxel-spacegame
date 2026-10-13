using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-33 §4.5/5.11 ownership surface; ENC-005 owns authored tables, ENC-014 adds procedural territory.</summary>
    public sealed class TerritoryService : IStarSystemOwnership, IGalaxyFrameTick
    {
        // TODO(ENC-005): catalog ownership and generation-aware rebuilds; no alternate galaxy data.
        public TerritoryService(GalaxyService galaxy, FactionService factions, ContentCatalog catalog) => throw new System.NotImplementedException("ENC-005");
        public int TickOrder => 310;
        public byte GetOwnerFaction(StarSystemId id) => throw new System.NotImplementedException("ENC-005");
        public string GetOwnerColorToken(StarSystemId id) => throw new System.NotImplementedException("ENC-005");
        public FactionDefinition GetOwner(StarSystemId id) => throw new System.NotImplementedException("ENC-005");
        public void Tick(in GalaxyFrameContext ctx) => throw new System.NotImplementedException("ENC-005");
    }
}
