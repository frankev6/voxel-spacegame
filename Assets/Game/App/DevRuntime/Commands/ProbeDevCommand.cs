#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.App.DevRuntime.Playground;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-70 DEV-024: ask the colliders about the flight plane instead of guessing from exported voxels. One point: clearance
    /// across the plane (walls at flight height), floor and roof, whether the player can fly there. A line (x2, z2): the first thing on it. find: the clearest reachable spots in a
    /// square that meet the conditions, best first.</summary>
    public sealed class ProbeDevCommand : IDevCommand
    {
        const int MaxCells = 250_000;
        public string Name => "probe";
        public string Usage => "probe x=<m> z=<m> [x2=<m> z2=<m>] [find=1 radius=200 clear=30 count=5 routes=<m> roof=1] [reach=8] [step=10] [hollow=1] [ships=1]";
        public string Summary => "Clearance, floor/roof, line of sight and reachability on the flight plane; find spots that fit (hollow=1: Hollow coordinates)";

        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "x", "z", "x2", "z2", "find", "radius", "clear", "count", "routes", "roof", "reach", "step", "hollow", "ships");
            if (error != null || args.Positional.Count != 0 || !args.Has("x") || !args.Has("z")) return DevCommandResult.Fail(error ?? Usage);
            if (args.Has("x2") != args.Has("z2")) return DevCommandResult.Fail("x2 and z2 go together");
            bool find = args.GetDouble("find", 0) != 0, roof = args.GetDouble("roof", 0) != 0, hollow = args.GetDouble("hollow", 0) != 0;
            if (args.Has("routes") && !(find && hollow)) return DevCommandResult.Fail("routes is for find with hollow=1 (the Hollow's traffic routes)");
            float step = (float)args.GetDouble("step", 10), reach = (float)args.GetDouble("reach", 8), radius = (float)args.GetDouble("radius", 200);
            float clear = (float)args.GetDouble("clear", 30), routes = (float)args.GetDouble("routes", 0);
            int count = (int)args.GetDouble("count", 5);
            if (!(step >= 1) || !(reach >= 0) || !(radius > 0) || !(clear > 0) || !(routes >= 0) || count < 1 || count > 50)
                return DevCommandResult.Fail("step ≥ 1, reach ≥ 0, radius > 0, clear > 0, routes ≥ 0, count 1..50");
            if (!ctx.HasGameplay || !ctx.TryGetFrame(out var frame)) return DevCommandResult.Fail("gameplay unavailable");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            if (!DevProbe.TryCreate(em, args.GetDouble("ships", 0) != 0, out var probe)) return DevCommandResult.Fail("no physics world");

            // the space the numbers are in: local frame, or the Hollow's own (its layout coordinates)
            float2 toLocal = 0;
            if (hollow)
            {
                var pg = ctx.Host?.Playground;
                if (pg == null) return DevCommandResult.Fail("hollow=1 needs the playground");
                toLocal = (float2)(pg.Origin.xz - frame.OriginInStarSystem.xz) + HollowLayout.Offset;
            }
            var at = new float2((float)args.GetDouble("x", 0), (float)args.GetDouble("z", 0));
            float2 player = float.NaN;
            if (ctx.PlayerShipRoot != Entity.Null && em.HasComponent<LocalTransform>(ctx.PlayerShipRoot))
                player = em.GetComponentData<LocalTransform>(ctx.PlayerShipRoot).Position.xz - toLocal;

            var text = new StringBuilder();
            if (args.Has("x2"))
            {
                var to = new float2((float)args.GetDouble("x2", 0), (float)args.GetDouble("z2", 0));
                bool blocked = probe.Ray(Plane(at + toLocal), Plane(to + toLocal), out float d, out var hit);
                text.Append(blocked ? "blocked at " + F(d) + " m of " + F(math.distance(at, to)) + " m by " + Describe(em, hit) + ", at " + P(at + math.normalizesafe(to - at) * d)
                                    : "clear line, " + F(math.distance(at, to)) + " m");
                return DevCommandResult.Ok(text.ToString());
            }
            if (!find)
            {
                float c = probe.FlatClearance(Plane(at + toLocal), DevProbe.Reach, out var near);
                text.Append(P(at)).Append(c < 0 ? ": inside " + Describe(em, near) + ", " + F(-c) + " m deep" : ": clear " + (c >= DevProbe.Reach ? "> " + F(DevProbe.Reach) : F(c)) + " m" + (near != Entity.Null ? " (nearest " + Describe(em, near) + ")" : ""));
                text.Append("; floor ").Append(Vertical(probe, at + toLocal, -1)).Append(", roof ").Append(Vertical(probe, at + toLocal, 1));
                if (reach > 0) text.Append("; ").Append(Reachable(probe, player, new[] { at }, toLocal, step, reach, out _)[0] ? "reachable" : "not reachable").Append(" from you (radius ").Append(F(reach)).Append(')');
                return DevCommandResult.Ok(text.ToString());
            }

            // find: the cells of the square that are clear enough, far enough from the routes and reachable, clearest first
            int n = (int)math.ceil(2 * radius / step);
            if (n * n > MaxCells) return DevCommandResult.Fail("too many cells: raise step or lower radius");
            var min = at - radius;
            var clearance = probe.Scan(min + toLocal, n, n, step, clear * 2, flat: true);
            var spots = new List<(float2 p, float clear, float route)>();
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                {
                    float c = clearance[k * n + i];
                    if (c < clear) continue;
                    var p = min + new float2(i + .5f, k + .5f) * step;
                    float r = routes > 0 ? RouteDistance(p) : float.PositiveInfinity;
                    if (r < routes || roof && Vertical(probe, p + toLocal, 1) == "none") continue;   // roof: under the rock only
                    spots.Add((p, c, r));
                }
            spots.Sort((a, b) => b.clear != a.clear ? b.clear.CompareTo(a.clear) : math.distancesq(a.p, at).CompareTo(math.distancesq(b.p, at)));
            string why = null;
            bool[] ok = reach > 0 && spots.Count > 0 ? Reachable(probe, player, spots.ConvertAll(s => s.p).ToArray(), toLocal, step, reach, out why) : null;
            if (ok != null && why != null) text.Append(why).Append(". ");
            int shown = 0, unreachable = 0;
            var picked = new List<float2>();
            for (int s = 0; s < spots.Count && shown < count; s++)
            {
                var (p, c, r) = spots[s];
                if (ok != null && !ok[s]) { unreachable++; continue; }
                bool close = false;   // one spot per clearing: skip neighbours of one already listed
                foreach (var q in picked) if (math.distance(q, p) < clear * 2) { close = true; break; }
                if (close) continue;
                picked.Add(p); shown++;
                text.Append('\n').Append(P(p)).Append(" clear ").Append(c >= clear * 2 ? "≥ " + F(clear * 2) : F(c)).Append(" m");
                if (routes > 0) text.Append(", route ").Append(F(r)).Append(" m");
                text.Append(", floor ").Append(Vertical(probe, p + toLocal, -1));
            }
            text.Insert(0, shown + " of " + spots.Count + " spots with " + F(clear) + " m clear" + (routes > 0 ? ", " + F(routes) + " m off the routes" : "") + (roof ? ", under a roof" : "")
                + (ok != null ? ", reachable" + (unreachable > 0 ? " (" + unreachable + " unreachable skipped)" : "") : "") + " within " + F(radius) + " m of " + P(at) + ":");
            return DevCommandResult.Ok(text.ToString());
        }

        /// <summary>For each point: can a ship of <paramref name="radius"/> fly there from the player along the flight plane (a flood over
        /// the box holding both, padded by 200 m).</summary>
        static bool[] Reachable(in DevProbe probe, float2 player, float2[] points, float2 toLocal, float step, float radius, out string why)
        {
            var result = new bool[points.Length]; why = null;
            if (math.any(math.isnan(player))) { why = "no player to reach from"; return result; }
            float2 lo = player, hi = player;
            foreach (var p in points) { lo = math.min(lo, p); hi = math.max(hi, p); }
            lo -= 200; hi += 200;
            float cell = math.min(step, radius * 2);
            int nx = (int)math.ceil((hi.x - lo.x) / cell), nz = (int)math.ceil((hi.y - lo.y) / cell);
            if (nx * nz > MaxCells) { why = "too far to check reachability"; return result; }
            var clear = probe.Scan(lo + toLocal, nx, nz, cell, radius + cell);
            int Index(float2 p) { var c = (int2)math.floor((p - lo) / cell); return math.all(c >= 0 & c < new int2(nx, nz)) ? c.y * nx + c.x : -1; }
            var reached = DevProbe.Flood(clear, nx, nz, Index(player), radius);
            if (Index(player) < 0 || !reached[Index(player)]) why = "your ship is not in a cell with " + F(radius) + " m clear";
            for (int i = 0; i < points.Length; i++) { int c = Index(points[i]); result[i] = c >= 0 && reached[c]; }
            return result;
        }

        /// <summary>Nearest Hollow traffic route (its polyline) to a Hollow point.</summary>
        static float RouteDistance(float2 p)
        {
            float best = float.PositiveInfinity;
            foreach (var route in HollowLayout.Traffic)
            {
                var pts = route.Points;
                for (int i = 0; i + 1 < pts.Length + (route.Loop ? 1 : 0); i++)
                {
                    float2 a = pts[i], b = pts[(i + 1) % pts.Length], ab = b - a;
                    float t = math.saturate(math.dot(p - a, ab) / math.max(1e-6f, math.lengthsq(ab)));
                    best = math.min(best, math.distance(p, a + ab * t));
                }
            }
            return best;
        }

        static string Vertical(in DevProbe probe, float2 at, float sign)
        {
            var from = Plane(at);
            return probe.Ray(from, from + new float3(0, sign * 300, 0), out float d, out _) ? "y " + F(sign * d) : "none";
        }

        static string Describe(EntityManager em, Entity e)
        {
            if (e == Entity.Null || !em.Exists(e)) return "nothing";
            string kind = em.HasComponent<VoxelGrid>(e) ? em.GetComponentData<VoxelGrid>(e).Kind.ToString() : "body";
            return kind + " #" + e.Index + (em.HasComponent<Game.Simulation.Spatial.SpatialScenery>(e) ? " (scenery)" : "");
        }

        static float3 Plane(float2 p) => new float3(p.x, 0, p.y);
        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static string P(float2 p) => "(" + F(p.x) + ", " + F(p.y) + ")";
    }
}
#endif
