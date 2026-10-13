using System.Collections.Generic;
using Game.Core;
using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Streaming
{
    /// <summary>Visits only rings and angular spans intersecting the streaming bubble; IDs retain the 4096 ring stride.</summary>
    internal static class BeltSectorGeometry
    {
        internal const double Tau = 6.2831853071795864769;
        internal static int RingCount(in BodySpec belt, float size)
            => (int)math.ceil((belt.BeltOuterRadiusM - belt.BeltInnerRadiusM) / size);
        internal static void Decode(in BodySpec belt, float size, int sector, out int ring, out int angular)
        {
            bool singleRing = RingCount(belt, size) == 1;
            ring = singleRing ? 0 : sector / 4096;
            angular = singleRing ? sector : sector % 4096;
        }
        internal static void Ring(in BodySpec belt, float size, int ring, out double inner, out double outer, out int count)
        {
            inner = belt.BeltInnerRadiusM + ring * (double)size;
            outer = math.min(belt.BeltOuterRadiusM, inner + size);
            count = (int)math.ceil(Tau * (inner + size * .5) / size);
        }
        internal static double3 Centre(in BodySpec belt, float size, int sector)
        {
            Decode(belt, size, sector, out int ring, out int angular);
            Ring(belt, size, ring, out double inner, out _, out int count);
            double angle = (angular + .5) * Tau / count;
            double radius = inner + size * .5;
            return new double3(math.cos(angle) * radius, 0, math.sin(angle) * radius);
        }
        internal static void CollectWanted(in BodySpec belt, float size, double3 player, float bubble, List<int> output)
        {
            output.Clear();
            if (size <= 0 || bubble <= 0 || math.abs(player.y) > bubble) return;
            int rings = RingCount(belt, size);
            int angularLimit = rings == 1 ? 8192 : 4096;
            if (rings > 2) WarnOverflow();
            double planarRadius = math.sqrt(math.max(0, (double)bubble * bubble - player.y * player.y));
            double distance = math.length(player.xz);
            int first = math.max(0, (int)math.floor((distance - planarRadius - belt.BeltInnerRadiusM) / size));
            int last = math.min(math.min(rings - 1, 1), (int)math.floor((distance + planarRadius - belt.BeltInnerRadiusM) / size));
            double angle = math.atan2(player.z, player.x);
            if (angle < 0) angle += Tau;
            for (int ring = first; ring <= last; ring++)
            {
                Ring(belt, size, ring, out double inner, out double outer, out int count);
                if (count > angularLimit) WarnOverflow();
                double radius = inner + size * .5;
                double halfAngle;
                if (distance == 0)
                {
                    if (radius > planarRadius) continue;
                    halfAngle = Tau * .5;
                }
                else
                {
                    double cosine = (radius * radius + distance * distance - planarRadius * planarRadius) / (2 * radius * distance);
                    if (cosine > 1) continue;
                    halfAngle = math.acos(math.clamp(cosine, -1, 1));
                }
                int start = (int)math.ceil((angle - halfAngle) * count / Tau - .5);
                int end = (int)math.floor((angle + halfAngle) * count / Tau - .5);
                for (int step = 0; step < math.min(count, end - start + 1); step++)
                {
                    int angular = ((start + step) % count + count) % count;
                    if (angular >= angularLimit) continue; // Never alias an unsupported angle into the next ring.
                    int sector = ring * 4096 + angular;
                    if (math.distancesq(player, Centre(belt, size, sector)) <= (double)bubble * bubble) output.Add(sector);
                }
            }
            output.Sort(); // Scheduling/spawn order does not change when the angular span crosses zero.
        }
        static void WarnOverflow() => GameLog.WarnOnce("Galaxy.BeltSectorOverflow", "Galaxy", "Skipping asteroid sectors outside the supported body subslot range.");
    }
}
