#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using Game.Core.Components;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.App.DevRuntime.Scenarios
{
    public static class GridScenarioProbes
    {
        sealed class Probe : IScenarioProbe
        {
            public string Name { get; }
            public int ArgCount { get; }
            internal Probe(string name, int args = 0) { Name = name; ArgCount = args; }
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args)
            {
                if (ctx.World != null && ctx.World.IsCreated) ctx.EntityManager.CompleteAllTrackedJobs();
                if (Name == "grids.count")
                {
                    if (!Enum.TryParse(RequireString(args[0]), true, out GridKind kind) || !Enum.IsDefined(typeof(GridKind), kind)) throw new InvalidOperationException("unknown grid kind");
                    if (ctx.World == null || !ctx.World.IsCreated) return Number(0);
                    int count = 0; using var roots = ctx.GridEntities();
                    foreach (var entity in roots) if (ctx.EntityManager.GetComponentData<VoxelGrid>(entity).Kind == kind) count++;
                    return Number(count);
                }
                Entity root = Name.StartsWith("grid.", StringComparison.Ordinal) ? DevEcs.FindGrid(ctx, Id(args[0])) : ctx.PlayerShipRoot;
                if (Name == "grid.exists") return Boolean(root != Entity.Null);
                if (Name == "grid.kind") return Text(root == Entity.Null ? "" : ctx.EntityManager.GetComponentData<VoxelGrid>(root).Kind.ToString());
                if (Name == "grid.cells" || Name == "player.cells") return Number(root == Entity.Null ? 0 : DevEcs.SolidCells(ctx.EntityManager, root));
                if (Name == "player.id") return Number(root != Entity.Null && ctx.EntityManager.HasComponent<PersistentId>(root) ? ctx.EntityManager.GetComponentData<PersistentId>(root).Value : 0);
                if (Name == "player.speed") return Number(root != Entity.Null && ctx.EntityManager.HasComponent<PhysicsVelocity>(root) ? Unity.Mathematics.math.length(ctx.EntityManager.GetComponentData<PhysicsVelocity>(root).Linear) : 0);
                if (root == Entity.Null) throw new InvalidOperationException("player coordinates unavailable");
                if (Name.StartsWith("player.upos.", StringComparison.Ordinal))
                {
                    if (!DevEcs.TryGetUniversePosition(ctx, root, out var up)) throw new InvalidOperationException("player universe position unavailable");
                    return Number(Name.EndsWith(".x", StringComparison.Ordinal) ? up.x : up.z);
                }
                if (!ctx.EntityManager.HasComponent<LocalTransform>(root)) throw new InvalidOperationException("player transform unavailable");
                var position = ctx.EntityManager.GetComponentData<LocalTransform>(root).Position;
                return Number(Name.EndsWith(".x", StringComparison.Ordinal) ? position.x : Name.EndsWith(".y", StringComparison.Ordinal) ? position.y : position.z);
            }
        }
        static ulong Id(ScenarioValue value)
        {
            if (value.Kind == ScenarioValueKind.String && ulong.TryParse(value.String, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id)) return id;
            double number = RequireNumber(value);
            if (!double.IsFinite(number) || number < 0 || number >= 18446744073709551616d || Math.Truncate(number) != number) throw new InvalidOperationException("grid ID must be an unsigned integer");
            return (ulong)number;
        }
        internal static void Register(ScenarioProbeRegistry registry)
        {
            foreach (string name in new[] { "player.pos.x", "player.pos.y", "player.pos.z", "player.upos.x", "player.upos.z", "player.speed", "player.id", "player.cells" }) registry.Register(new Probe(name));
            foreach (string name in new[] { "grid.exists", "grid.cells", "grid.kind", "grids.count" }) registry.Register(new Probe(name, 1));
        }
    }
}
#endif
