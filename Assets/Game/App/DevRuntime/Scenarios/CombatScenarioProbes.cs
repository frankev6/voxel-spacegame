#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation;
using Game.Galaxy.Services;
using Game.Simulation.Factions;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Entities;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>DEV-027: what a fight did, for repro scenarios that report numbers. The player's hull (ShipStats.MassFraction, the HUD's
    /// meter, 0..1), shield points and whether it is a hulk; per grid kind (Ship, Station, Asteroid, Hulk, Device), the damage taken, the
    /// cells knocked loose and how many grids took any (GridDamageStats, cumulative since each grid spawned); the sim clock in
    /// seconds; and how many of a faction's ships still fly (ships.alive, not hulks).</summary>
    public static class CombatScenarioProbes
    {
        sealed class Probe : IScenarioProbe
        {
            public string Name { get; }
            public int ArgCount { get; }
            internal Probe(string name, int args = 0) { Name = name; ArgCount = args; }
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args)
            {
                if (Name == "sim.seconds") return Number(ctx.Tick < 0 ? -1 : ctx.Tick / SimTime.TicksPerSecond);
                if (ctx.World == null || !ctx.World.IsCreated) return Name == "player.dead" ? Boolean(false) : Number(0);
                var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
                if (Name.StartsWith("damage.", StringComparison.Ordinal))
                {
                    if (!Enum.TryParse(RequireString(args[0]), true, out GridKind kind) || !Enum.IsDefined(typeof(GridKind), kind)) throw new InvalidOperationException("unknown grid kind");
                    double sum = 0; int grids = 0;
                    using var roots = ctx.GridEntities();
                    foreach (var e in roots)
                    {
                        if (em.GetComponentData<VoxelGrid>(e).Kind != kind || !em.HasComponent<GridDamageStats>(e)) continue;
                        var stats = em.GetComponentData<GridDamageStats>(e);
                        if (Name == "damage.taken") sum += stats.DamageTaken;
                        else if (Name == "damage.detached") sum += stats.CellsDetached;
                        else if (stats.DamageTaken > 0) grids++;
                    }
                    return Number(Name == "damage.grids" ? grids : sum);
                }
                if (Name == "ships.alive")
                {
                    if (!ctx.TryGetService<ContentService>(out var content) || !content.Catalog.TryGet<FactionDefinition>(new ContentId(RequireString(args[0])), out var faction))
                        throw new InvalidOperationException("unknown faction");
                    FactionIndexMap indices;
                    if (ctx.TryGetService<FactionService>(out var factions)) indices = factions.Indices;
                    else if (!ctx.TryGetService(out indices)) throw new InvalidOperationException("faction index map unavailable");
                    byte index = indices.IndexOf(faction); int count = 0;
                    using var roots = ctx.GridEntities();
                    foreach (var e in roots)
                        if (em.GetComponentData<VoxelGrid>(e).Kind == GridKind.Ship && !em.HasComponent<Hulk>(e) && em.HasComponent<Allegiance>(e)
                            && em.GetComponentData<Allegiance>(e).Faction == index) count++;
                    return Number(count);
                }
                var player = ctx.PlayerShipRoot;
                bool alive = player != Entity.Null && em.Exists(player);
                switch (Name)
                {
                    case "player.dead": return Boolean(alive && em.HasComponent<Hulk>(player));
                    case "player.hull": return Number(alive && em.HasComponent<ShipStats>(player) ? em.GetComponentData<ShipStats>(player).MassFraction : 0);
                    case "player.shield": return Number(alive && em.HasComponent<ShieldState>(player) ? em.GetComponentData<ShieldState>(player).Hp : 0);
                }
                throw new InvalidOperationException("unknown probe " + Name);
            }
        }

        internal static void Register(ScenarioProbeRegistry registry)
        {
            foreach (string name in new[] { "player.dead", "player.hull", "player.shield", "sim.seconds" }) registry.Register(new Probe(name));
            foreach (string name in new[] { "damage.taken", "damage.detached", "damage.grids", "ships.alive" }) registry.Register(new Probe(name, 1));
        }
    }
}
#endif
