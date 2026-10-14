using System;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-33 §4.5/5.11 ownership surface; ENC-005 owns authored tables, ENC-014 adds procedural territory.</summary>
    public sealed class TerritoryService : IStarSystemOwnership, IGalaxyFrameTick
    {
        readonly GalaxyService galaxy;
        readonly FactionService factions;
        byte[] owners = Array.Empty<byte>();
        int generation = -1;
        public TerritoryService(GalaxyService galaxy, FactionService factions, ContentCatalog catalog)
        {
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.factions = factions ?? throw new ArgumentNullException(nameof(factions));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            RebuildIfNeeded();
        }
        public int TickOrder => 310;
        public byte GetOwnerFaction(StarSystemId id)
        {
            RebuildIfNeeded();
            return galaxy.Galaxy != null && galaxy.Galaxy.TryGetIndex(id, out int index) ? owners[index] : (byte)255;
        }
        public string GetOwnerColorToken(StarSystemId id) => GetOwner(id)?.MapColorToken ?? "";
        public FactionDefinition GetOwner(StarSystemId id) => factions.Indices.ByIndex(GetOwnerFaction(id));
        public void Tick(in GalaxyFrameContext ctx) => RebuildIfNeeded();
        void RebuildIfNeeded()
        {
            var definition = galaxy.Galaxy;
            if (definition == null)
            {
                owners = Array.Empty<byte>(); generation = -1;
                return;
            }
            if (generation == galaxy.GenerationCount) return;
            if (owners.Length != definition.StarSystems.Length) owners = new byte[definition.StarSystems.Length];
            Array.Fill(owners, (byte)255);
            var indices = factions.Indices;
            for (byte index = 0; index < indices.Count; index++)
            {
                var faction = indices.ByIndex(index);
                if (faction == null || faction.OwnedStarSystems == null) continue;
                foreach (var authored in faction.OwnedStarSystems)
                    if (authored != null && galaxy.TryResolveStarSystem(authored.Id, out var id) && definition.TryGetIndex(id, out int system))
                        owners[system] = index;
            }
            // ENC-014 adds procedural ownership and TerritoryEntered; M3 retains the authored table only.
            generation = galaxy.GenerationCount;
        }
    }
}
