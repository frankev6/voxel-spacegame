#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.App.DevRuntime
{
    /// <summary>SPEC-70 DEV-024: what the colliders say about the flight plane, for placing things in big built places (the Hollow): how
    /// far the nearest solid is, the floor and roof above and below a point, the first thing on a line, and a scan for spots that fit.
    /// Main thread only (it reads the EntityManager); call after <see cref="EntityManager.CompleteAllTrackedJobs"/>. Ships, hulks and
    /// devices are ignored unless <c>ships</c> is set, so traffic does not count as a wall.</summary>
    internal readonly struct DevProbe
    {
        internal const float Reach = 2000;   // furthest a clearance or floor query looks
        readonly CollisionWorld world; readonly EntityManager em; readonly bool ships;
        internal DevProbe(in CollisionWorld world, EntityManager em, bool ships) { this.world = world; this.em = em; this.ships = ships; }

        internal static bool TryCreate(EntityManager em, bool ships, out DevProbe probe)
        {
            probe = default;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            if (!q.TryGetSingleton<PhysicsWorldSingleton>(out var physics)) return false;
            probe = new DevProbe(physics.CollisionWorld, em, ships); return true;
        }

        /// <summary>Signed distance from <paramref name="at"/> to the nearest counted surface within <paramref name="max"/> (negative:
        /// inside it; <paramref name="max"/> when nothing is near).</summary>
        internal float Clearance(float3 at, float max, out Entity nearest)
        {
            var collector = new NearestDistance { Probe = this, MaxFraction = max, Distance = max };
            world.CalculateDistance(new PointDistanceInput { Position = at, MaxDistance = max, Filter = PhysicsLayers.NavigationQuery }, ref collector);
            nearest = collector.Entity; return collector.Distance;
        }

        /// <summary>Clearance across the flight plane: the nearest wall at this height, ignoring the floor and roof (32 rays; inside a
        /// solid, the negative depth). The 3D distance is a lower bound, so it answers alone when nothing is within <paramref name="max"/>.
        /// </summary>
        internal float FlatClearance(float3 at, float max, out Entity nearest)
        {
            float d = Clearance(at, max, out nearest);
            if (d < 0 || d >= max) return d;
            float best = max; Entity hit = Entity.Null;
            for (int r = 0; r < 32; r++)
            {
                math.sincos(r * (math.PI2 / 32), out float s, out float c);
                if (Ray(at, at + new float3(s, 0, c) * best, out float along, out var e) && along < best) { best = along; hit = e; }
            }
            if (hit != Entity.Null) nearest = hit;
            return math.max(best, d);
        }

        /// <summary>The first counted surface from <paramref name="from"/> to <paramref name="to"/>: its distance and entity.</summary>
        internal bool Ray(float3 from, float3 to, out float distance, out Entity hit)
        {
            var collector = new FirstHit { Probe = this, MaxFraction = 1, Fraction = 2 };
            world.CastRay(new RaycastInput { Start = from, End = to, Filter = PhysicsLayers.NavigationQuery }, ref collector);
            hit = collector.Entity; distance = collector.Fraction <= 1 ? collector.Fraction * math.distance(from, to) : float.PositiveInfinity;
            return collector.Fraction <= 1;
        }

        internal bool Counts(Entity e)
        {
            if (ships || !em.Exists(e) || !em.HasComponent<VoxelGrid>(e)) return true;
            var kind = em.GetComponentData<VoxelGrid>(e).Kind;
            return kind == GridKind.Station || kind == GridKind.Asteroid;
        }

        /// <summary>A grid of clearances on the flight plane (<paramref name="flat"/>: across the plane, see <see cref="FlatClearance"/>;
        /// otherwise the 3D distance, a lower bound that is enough for reachability): <paramref name="nx"/> × <paramref name="nz"/> cells of
        /// <paramref name="step"/> metres from <paramref name="min"/> (cell centres), row-major in x.</summary>
        internal float[] Scan(float2 min, int nx, int nz, float step, float max, bool flat = false)
        {
            var clear = new float[nx * nz];
            for (int k = 0; k < nz; k++)
                for (int i = 0; i < nx; i++)
                    {
                    var at = new float3(min.x + (i + .5f) * step, 0, min.y + (k + .5f) * step);
                    clear[k * nx + i] = flat ? FlatClearance(at, max, out _) : Clearance(at, max, out _);
                }
            return clear;
        }

        /// <summary>The cells reachable from <paramref name="start"/> through cells with at least <paramref name="radius"/> clearance
        /// (4-connected; a ship of that radius can move between neighbouring cells when both are that clear and step ≤ 2 × radius).</summary>
        internal static bool[] Flood(float[] clear, int nx, int nz, int start, float radius)
        {
            var reached = new bool[clear.Length];
            if (start < 0 || start >= clear.Length || clear[start] < radius) return reached;
            var open = new System.Collections.Generic.Queue<int>(); open.Enqueue(start); reached[start] = true;
            while (open.Count > 0)
            {
                int c = open.Dequeue(), i = c % nx, k = c / nx;
                Visit(i - 1, k); Visit(i + 1, k); Visit(i, k - 1); Visit(i, k + 1);
            }
            return reached;

            void Visit(int i, int k)
            {
                if (i < 0 || k < 0 || i >= nx || k >= nz) return;
                int c = k * nx + i;
                if (reached[c] || clear[c] < radius) return;
                reached[c] = true; open.Enqueue(c);
            }
        }

        struct NearestDistance : ICollector<DistanceHit>
        {
            public DevProbe Probe; public float Distance; public Entity Entity;
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; set; }
            public int NumHits { get; private set; }
            public bool AddHit(DistanceHit hit)
            {
                if (hit.Distance >= Distance || !Probe.Counts(hit.Entity)) return false;
                Distance = hit.Distance; Entity = hit.Entity; NumHits = 1;
                if (hit.Distance > 0) MaxFraction = hit.Distance;   // inside a box: keep looking for deeper ones
                return true;
            }
        }

        struct FirstHit : ICollector<RaycastHit>
        {
            public DevProbe Probe; public float Fraction; public Entity Entity;
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; set; }
            public int NumHits { get; private set; }
            public bool AddHit(RaycastHit hit)
            {
                if (hit.Fraction >= Fraction || !Probe.Counts(hit.Entity)) return false;
                Fraction = hit.Fraction; Entity = hit.Entity; MaxFraction = hit.Fraction; NumHits = 1; return true;
            }
        }
    }
}
#endif
