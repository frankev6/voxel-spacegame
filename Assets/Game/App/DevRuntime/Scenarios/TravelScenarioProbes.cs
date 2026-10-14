#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Galaxy.Services;
using Unity.Mathematics;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>SPEC-32 TRV-011: diagnostics from real travel, loader and player position owners.</summary>
    public static class TravelScenarioProbes
    {
        sealed class Probe : IScenarioProbe
        {
            public string Name { get; }
            public int ArgCount => 0;
            internal Probe(string name) => Name = name;
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args)
            {
                bool hasTravel = ctx.TryGetService<TravelService>(out var travel);
                if (Name == "travel.phase") return Text(hasTravel ? travel.Phase.ToString() : "unavailable");
                if (Name == "travel.destination") return Number(hasTravel && travel.Destination.IsValid ? travel.Destination.Value : -1d);
                if (Name == "travel.route_hops") return Number(hasTravel ? travel.Route.Count : 0);
                if (ctx.World == null || !ctx.World.IsCreated)
                    return Name == "travel.inside_ring" ? Boolean(false) : Number(-1);
                ctx.EntityManager.CompleteAllTrackedJobs();
                if (!DevEcs.TryGetUniversePosition(ctx, ctx.PlayerShipRoot, out var position))
                    return Name == "travel.inside_ring" ? Boolean(false) : Number(-1);
                if (Name == "travel.arrival_gap")
                {
                    if (!hasTravel || travel.LastArrival.GateKey == 0) return Number(-1);
                    var plan = travel.LastArrival;
                    return Number(math.length((position - plan.GatePositionM).xz) - plan.GateRadiusM);
                }
                if (!ctx.TryGetService<StarSystemLoader>(out var loader) || !loader.IsReady || !loader.Loaded.IsValid ||
                    !ctx.TryGetService<GalaxyService>(out var galaxy) || galaxy.Galaxy == null) return Boolean(false);
                return Boolean(math.length(position.xz) <= galaxy.GetContent(loader.Loaded).LaneRingRadiusM);
            }
        }
        internal static void Register(ScenarioProbeRegistry registry)
        {
            foreach (string name in new[] { "travel.phase", "travel.arrival_gap", "travel.inside_ring", "travel.destination", "travel.route_hops" })
                registry.Register(new Probe(name));
        }
    }
}
#endif
