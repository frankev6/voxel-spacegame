using System;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-33 §4.5/5.11 ownership surface; ENC-005 owns authored tables, ENC-014 adds procedural territory.</summary>
    public sealed class TerritoryService : IStarSystemOwnership, IGalaxyFrameTick, IDisposable
    {
        readonly GalaxyService galaxy;
        readonly FactionService factions;
        byte[] owners = Array.Empty<byte>();
        byte[] hops = Array.Empty<byte>();
        int[] procedural = Array.Empty<int>(), queue = Array.Empty<int>(), radii = Array.Empty<int>();
        int generation = -1;
        StarSystemId previousSystem;
        bool disposed;
        public TerritoryService(GalaxyService galaxy, FactionService factions, ContentCatalog catalog)
        {
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.factions = factions ?? throw new ArgumentNullException(nameof(factions));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            RebuildIfNeeded();
            EventBus<StarSystemEntered>.Subscribe(OnEntered);
            EventBus<SessionReset>.Subscribe(OnReset);
        }
        public int TickOrder => 310;
        public byte GetOwnerFaction(StarSystemId id)
        {
            if (disposed) throw new ObjectDisposedException(nameof(TerritoryService));
            RebuildIfNeeded();
            return galaxy.Galaxy != null && galaxy.Galaxy.TryGetIndex(id, out int index) ? owners[index] : (byte)255;
        }
        public string GetOwnerColorToken(StarSystemId id) => GetOwner(id)?.MapColorToken ?? "";
        public FactionDefinition GetOwner(StarSystemId id) => factions.Indices.ByIndex(GetOwnerFaction(id));
        public void Tick(in GalaxyFrameContext ctx) { if (!disposed) RebuildIfNeeded(); }
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
            FillProcedural(definition);
            generation = galaxy.GenerationCount;
        }
        void FillProcedural(GalaxyDef definition)
        {
            int count = definition.StarSystems.Length;
            if (queue.Length < count) { queue = new int[count]; procedural = new int[count]; hops = new byte[count]; }
            int factionCount = factions.Indices.Count;
            if (radii.Length < factionCount) radii = new int[factionCount];
            int available = 0;
            for (int i = 0; i < count; i++)
                if (definition.StarSystems[i].AuthoredIndex == 0) procedural[available++] = i;
            if (available == 0) return;
            int tail = 0, free = available;
            // Enqueue every seed before expansion, in canonical faction order. FIFO preserves
            // that order at each distance layer, so the lower index wins equal-hop claims.
            for (byte faction = 0; faction < factionCount; faction++)
            {
                var data = factions.Indices.ByIndex(faction);
                radii[faction] = data == null ? 0 : Math.Clamp(data.TerritoryRadiusHops, 0, 8);
                if (data == null || data.TerritorySeeds <= 0) continue;
                var rng = RngStreams.For(definition.Seed, RngPurpose.Territory, faction);
                for (int seed = 0; seed < data.TerritorySeeds; seed++)
                {
                    int slot = rng.NextInt(0, available);
                    if (free == 0) continue; // Consume the prescribed draw, but never wrap indefinitely.
                    while (owners[procedural[slot]] != 255) { slot++; if (slot == available) slot = 0; }
                    int system = procedural[slot]; owners[system] = faction; hops[system] = 0;
                    queue[tail++] = system; free--;
                }
            }
            for (int head = 0; head < tail; head++)
            {
                int current = queue[head]; byte owner = owners[current], distance = hops[current];
                if (distance >= radii[owner]) continue;
                var system = definition.StarSystems[current];
                for (int edge = system.FirstAdjacency; edge < system.FirstAdjacency + system.LaneCount; edge++)
                {
                    var lane = definition.Lanes[definition.Adjacency[edge]];
                    var neighbour = GalaxyDef.Neighbour(lane, system.Id);
                    if (!definition.TryGetIndex(neighbour, out int next) || definition.StarSystems[next].AuthoredIndex != 0 || owners[next] != 255) continue;
                    owners[next] = owner; hops[next] = (byte)(distance + 1); queue[tail++] = next;
                }
            }
        }
        void OnEntered(StarSystemEntered value)
        {
            if (disposed) return;
            var next = new StarSystemId(value.StarSystemValue);
            byte owner = GetOwnerFaction(next), previous = GetOwnerFaction(previousSystem);
            previousSystem = next;
            // Restore the previous-owner baseline without announcing a new crossing.
            if (value.Reason == StarSystemEntryReason.Load) return;
            if (owner != 255 && owner != previous)
                EventBus<TerritoryEntered>.Publish(new TerritoryEntered(factions.Indices.ByIndex(owner).Id.Hash, next.Value));
        }
        void OnReset(SessionReset value) => previousSystem = default;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EventBus<StarSystemEntered>.Unsubscribe(OnEntered); EventBus<SessionReset>.Unsubscribe(OnReset);
            owners = hops = Array.Empty<byte>(); procedural = queue = radii = Array.Empty<int>(); previousSystem = default;
        }
    }
}
