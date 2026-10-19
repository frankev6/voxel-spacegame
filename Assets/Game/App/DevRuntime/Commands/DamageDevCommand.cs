#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using Game.Data.Combat;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>ABL-006 test aid: a blast on a grid at its rear, front or a grid-local point, through the damage channel (the same path
    /// as weapons), so a repair has something to rebuild. Rear and front are the ends of the grid's cell bounds along z.</summary>
    public sealed class DamageDevCommand : IDevCommand
    {
        public string Name => "damage";
        public string Usage => "damage player=true | id=<PersistentId> | nearest=true [at=rear|front|x,y,z] [radius=3] [amount=20000]";
        public string Summary => "Blast a grid (metres, grid-local) to test damage and repairs";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "id", "player", "nearest", "at", "radius", "amount");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            bool player = args.GetBool("player", false), nearest = args.GetBool("nearest", false);
            if ((args.Has("id") ? 1 : 0) + (player ? 1 : 0) + (nearest ? 1 : 0) != 1) return DevCommandResult.Fail(Usage);
            if (ctx.World == null || !ctx.World.IsCreated) return DevCommandResult.Fail("world unavailable");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            Entity root = player ? ctx.PlayerShipRoot : nearest ? DevEcs.NearestGrid(ctx) : DevEcs.FindGrid(ctx, args.GetULong("id", 0));
            if (root == Entity.Null || !em.HasComponent<VoxelGrid>(root)) return DevCommandResult.Fail("grid not found");
            var grid = em.GetComponentData<VoxelGrid>(root);
            float3 min = (float3)grid.CellMin * grid.CellSize, max = ((float3)grid.CellMax + 1) * grid.CellSize, centre = (min + max) * .5f;
            string at = args.GetString("at", "rear"); float3 point;
            if (at == "rear") point = new float3(centre.x, centre.y, min.z + 1);
            else if (at == "front") point = new float3(centre.x, centre.y, max.z - 1);
            else
            {
                var parts = at.Split(',');
                if (parts.Length != 3 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                    !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                    !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) return DevCommandResult.Fail("at expects rear, front or x,y,z");
                point = new float3(x, y, z);
            }
            float radius = (float)args.GetDouble("radius", 3), amount = (float)args.GetDouble("amount", 20000);
            if (!(radius > 0) || !(amount > 0)) return DevCommandResult.Fail("radius and amount must be positive");
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<DamageStreams>());
            if (query.CalculateEntityCount() != 1) return DevCommandResult.Fail("damage streams unavailable");
            var damage = query.GetSingleton<DamageStreams>();
            damage.Damage.PostFromMainThread(new DamageEvent { Grid = root, LocalPoint = point, Amount = amount, Radius = radius, Type = DamageType.Explosive,
                Producer = ProducerKind.MainThread, Sequence = (uint)System.Math.Max(0, ctx.Tick) });
            return DevCommandResult.Ok("blast " + amount.ToString("0", CultureInfo.InvariantCulture) + " r=" + radius.ToString("0.#", CultureInfo.InvariantCulture) +
                " at " + point.x.ToString("0.#", CultureInfo.InvariantCulture) + "," + point.y.ToString("0.#", CultureInfo.InvariantCulture) + "," + point.z.ToString("0.#", CultureInfo.InvariantCulture));
        }
    }
}
#endif
