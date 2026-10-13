using Unity.Mathematics;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.4. Pure, allocation-free, Burst-compatible ellipsoid queries in grid space.</summary>
    public static class ShieldMath
    {
        /// <summary>True only if p0→p1 starts outside and enters or touches; t is the entry fraction in [0,1], or 0 on a miss.
        /// Shots from inside or on the surface pass out. Finite inputs and positive radii are required.</summary>
        public static bool SegmentEnters(float3 p0, float3 p1, float3 center, float3 radii, out float t)
        {
            t = 0;
            double3 a = Offset(p0, center, radii);
            if (math.dot(a, a) <= 1) return false;
            double3 b = Offset(p1, center, radii);
            double3 d = b - a;
            if (math.dot(d, d) < 1e-12) return false;

            // Float inputs can span 83 orders of magnitude in ellipsoid space. Double preserves the offsets;
            // scaling the whole quadratic keeps its discriminant finite even at that range (SPEC-22 Q11).
            double scale = math.max(math.cmax(math.abs(a)), math.cmax(math.abs(d)));
            a /= scale; d /= scale;
            double unitRadius = 1 / scale;
            double quadraticA = math.dot(d, d);
            double quadraticB = 2 * math.dot(a, d);
            double quadraticC = math.dot(a, a) - unitRadius * unitRadius;
            double discriminant = quadraticB * quadraticB - 4 * quadraticA * quadraticC;
            if (discriminant < 0) return false;
            double entry = (-quadraticB - math.sqrt(discriminant)) / (2 * quadraticA);
            if (entry < 0 || entry > 1) return false;
            t = (float)entry;
            return true;
        }

        /// <summary>True if p is inside or on the ellipsoid.</summary>
        public static bool Contains(float3 p, float3 center, float3 radii)
        {
            double3 q = Offset(p, center, radii);
            return math.dot(q, q) <= 1;
        }

        /// <summary>Conservative test: true if a sphere with centre c and radius r may overlap the ellipsoid.</summary>
        public static bool SphereOverlaps(float3 c, float r, float3 center, float3 radii)
        {
            double3 q = Offset(c, center, radii);
            return math.length(q) <= 1 + (double)r / math.cmin(radii);
        }

        /// <summary>Surface point along the ray from center towards p; p must differ from center.</summary>
        public static float3 SurfacePointToward(float3 p, float3 center, float3 radii)
        {
            double3 q = Offset(p, center, radii);
            double length = math.length(q);
            // A centre query has no unique ray; the deterministic +X extension prevents 0/0 (SPEC-22 Q11).
            if (length == 0) return (float3)((double3)center + new double3(radii.x, 0, 0));
            return (float3)((double3)center + q / length * (double3)radii);
        }

        /// <summary>Outward unit normal at a surface point.</summary>
        public static float3 Normal(float3 surfacePoint, float3 center, float3 radii)
        {
            double3 normal = Offset(surfacePoint, center, radii) / (double3)radii;
            double length = math.length(normal);
            return length > 0 ? (float3)(normal / length) : new float3(1, 0, 0);
        }

        static double3 Offset(float3 point, float3 center, float3 radii)
            => ((double3)point - (double3)center) / (double3)radii;
    }
}
