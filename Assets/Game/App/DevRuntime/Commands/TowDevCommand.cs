#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using Game.Core.Components;
using Game.Simulation.Extraction;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-93 EXT-002: the magnet tow from the console. attach takes the nearest loose body (a dynamic grid that is not a ship).</summary>
    public sealed class TowDevCommand : IDevCommand
    {
        public string Name => "tow";
        public string Usage => "tow attach [range=80] [length=<m>] [id=<grid id>] | tow release | tow status";
        public string Summary => "Attach the player's magnet cable to a nearby loose body, release it, or show the cable";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "range", "length", "id");
            if (error != null) return DevCommandResult.Fail(error);
            if (!ctx.HasGameplay || ctx.PlayerShipRoot == Entity.Null) return DevCommandResult.Fail("player unavailable");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs(); var ship = ctx.PlayerShipRoot;
            string verb = args.Positional.Count > 0 ? args.Positional[0] : "status";
            switch (verb)
            {
                case "release":
                    if (!TowApi.IsTowing(em, ship)) return DevCommandResult.Fail("not towing");
                    TowApi.Release(em, ship); return DevCommandResult.Ok("released");
                case "status":
                    if (!TowApi.IsTowing(em, ship)) return DevCommandResult.Ok("towing=none");
                    return DevCommandResult.Ok(Status(em, ship));
                case "attach":
                    Entity load = Entity.Null;
                    if (args.Has("id")) load = DevEcs.FindGrid(ctx, (ulong)args.GetDouble("id", 0));
                    else load = Nearest(em, ship, (float)args.GetDouble("range", 80));
                    if (load == Entity.Null) return DevCommandResult.Fail("no loose body in range");
                    float3 a = em.GetComponentData<LocalTransform>(ship).Position, b = em.GetComponentData<LocalTransform>(load).Position;
                    float length = (float)args.GetDouble("length", math.max(12, math.distance(a, b) + 2));
                    uint tick = 0;
                    using (var q = em.CreateEntityQuery(typeof(SimClock))) if (q.CalculateEntityCount() == 1) tick = q.GetSingleton<SimClock>().Tick;
                    if (!TowApi.Attach(em, ship, load, length, tick)) return DevCommandResult.Fail("attach refused (already towed, or not a body)");
                    return DevCommandResult.Ok("attached " + Status(em, ship));
                default: return DevCommandResult.Fail(Usage);
            }
        }

        static string Status(EntityManager em, Entity ship)
        {
            var link = em.GetComponentData<TowLink>(ship);
            float massT = em.HasComponent<TowLoad>(link.Load) ? em.GetComponentData<TowLoad>(link.Load).MassT : 0;
            float d = math.distance(em.GetComponentData<LocalTransform>(ship).Position, em.GetComponentData<LocalTransform>(link.Load).Position);
            string id = em.HasComponent<PersistentId>(link.Load) ? em.GetComponentData<PersistentId>(link.Load).Value.ToString(CultureInfo.InvariantCulture) : link.Load.ToString();
            return "towing=" + id + " massT=" + massT.ToString("0.0", CultureInfo.InvariantCulture) + " length=" + link.Length.ToString("0.0", CultureInfo.InvariantCulture)
                + " distance=" + d.ToString("0.0", CultureInfo.InvariantCulture) + " tensionN=" + link.Tension.ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>The nearest dynamic grid that is not a live ship, within range of the ship's centre.</summary>
        public static Entity Nearest(EntityManager em, Entity ship, float range)
        {
            float3 at = em.GetComponentData<LocalTransform>(ship).Position;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<PhysicsMass>(), ComponentType.ReadOnly<PhysicsVelocity>(), ComponentType.ReadOnly<LocalTransform>());
            using var es = q.ToEntityArray(Allocator.Temp);
            Entity best = Entity.Null; float bestD = range;
            foreach (var e in es)
            {
                if (e == ship || em.GetComponentData<PhysicsMass>(e).InverseMass <= 0) continue;
                var g = em.GetComponentData<VoxelGrid>(e);
                if (g.Kind == GridKind.Ship && !em.HasComponent<Hulk>(e)) continue;
                float d = math.distance(at, em.GetComponentData<LocalTransform>(e).Position);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }
    }
}
#endif
