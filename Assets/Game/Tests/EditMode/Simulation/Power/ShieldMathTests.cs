using Game.Core;
using Game.Simulation.Power;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>PWR-002: specified ellipsoid formulas and an independently selected seeded march oracle (SPEC-22 §4.4, Q11–12).</summary>
    public sealed class ShieldMathTests
    {
        [Test]
        public void SegmentEnters_UnitSphere()
        {
            Assert.IsTrue(ShieldMath.SegmentEnters(new float3(-10, 0, 0), new float3(10, 0, 0), 0, 1, out var t));
            Assert.AreEqual(.45f, t, 1e-6f);
            Assert.IsTrue(ShieldMath.SegmentEnters(new float3(-2, 1, 0), new float3(2, 1, 0), 0, 1, out t));
            Assert.AreEqual(.5f, t, "The specified zero-discriminant rule includes tangency.");
            Assert.IsTrue(ShieldMath.SegmentEnters(new float3(-2, 0, 0), new float3(-1, 0, 0), 0, 1, out t));
            Assert.AreEqual(1f, t);
            Assert.IsFalse(ShieldMath.SegmentEnters(new float3(2, 2, 0), new float3(3, 2, 0), 0, 1, out t));
            Assert.AreEqual(0f, t);

            Assert.IsTrue(ShieldMath.SegmentEnters(new float3(-float.MaxValue, 0, 0), new float3(float.MaxValue, 0, 0),
                0, new float3(1e-30f), out t));
            Assert.IsTrue(math.isfinite(t));
            Assert.AreEqual(.5f, t, "The tiny entry offset rounds to one half in the public float result.");
            Assert.IsTrue(ShieldMath.SegmentEnters(new float3(-float.MaxValue, 0, 0), new float3(float.MaxValue, 0, 0),
                0, new float3(float.Epsilon), out t));
            Assert.AreEqual(.5f, t, "Scaling also keeps the quadratic finite for the smallest positive float radius.");
            Assert.IsTrue(ShieldMath.SegmentEnters(new float3(-2e-30f, 0, 0), new float3(2e-30f, 0, 0),
                0, new float3(1e-30f), out t));
            Assert.AreEqual(.25f, t, 1e-6f);
        }

        [Test]
        public void SegmentEnters_FromInside_False()
        {
            Assert.IsFalse(ShieldMath.SegmentEnters(0, new float3(10, 0, 0), 0, 1, out var t));
            Assert.AreEqual(0f, t);
            Assert.IsFalse(ShieldMath.SegmentEnters(new float3(1, 0, 0), new float3(2, 0, 0), 0, 1, out t));
            Assert.AreEqual(0f, t, "A start on the surface uses the specified inside rule.");
            Assert.IsFalse(ShieldMath.SegmentEnters(new float3(2, 0, 0), new float3(2, 0, 0), 0, 1, out t));
            Assert.AreEqual(0f, t, "A zero-length outside segment cannot enter.");
            Assert.IsFalse(ShieldMath.SegmentEnters(0, 0, 0, 1, out t));
            Assert.AreEqual(0f, t);
        }

        [Test]
        public void Contains_Ellipsoid()
        {
            var radii = new float3(4, 1, 2);
            Assert.IsTrue(ShieldMath.Contains(new float3(3.9f, 0, 0), 0, radii));
            Assert.IsFalse(ShieldMath.Contains(new float3(0, 1.1f, 0), 0, radii));
            Assert.IsTrue(ShieldMath.Contains(new float3(4, 0, 0), 0, radii));
            Assert.IsTrue(ShieldMath.Contains(0, 0, radii));

            var center = new float3(7, -3, 5);
            var surface = ShieldMath.SurfacePointToward(center + new float3(4, 2, 0), center, radii);
            var expectedSurface = center + new float3(4, 2, 0) / math.sqrt(5f);
            Assert.Less(math.distance(expectedSurface, surface), 1e-6f);
            var expectedNormal = math.normalize(new float3(1, 8, 0));
            Assert.Less(math.distance(expectedNormal, ShieldMath.Normal(surface, center, radii)), 1e-6f);
            Assert.AreEqual(center + new float3(4, 0, 0), ShieldMath.SurfacePointToward(center, center, radii));
            Assert.AreEqual(new float3(1, 0, 0), ShieldMath.Normal(center, center, radii));

            var tinyRadii = new float3(1e-30f);
            Assert.IsFalse(ShieldMath.Contains(new float3(float.MaxValue, 0, 0), 0, tinyRadii));
            Assert.AreEqual(new float3(1e-30f, 0, 0), ShieldMath.SurfacePointToward(new float3(float.MaxValue, 0, 0), 0, tinyRadii));
            Assert.AreEqual(new float3(1, 0, 0), ShieldMath.Normal(new float3(1e-30f, 0, 0), 0, tinyRadii));
        }

        [Test]
        public void SphereOverlaps_Conservative()
        {
            var center = new float3(7, -3, 5);
            var radii = new float3(4, 1, 2);
            Assert.IsTrue(ShieldMath.SphereOverlaps(center, 0, center, radii));
            Assert.IsTrue(ShieldMath.SphereOverlaps(center + new float3(0, 1.5f, 0), .5f, center, radii));
            Assert.IsTrue(ShieldMath.SphereOverlaps(center + new float3(4.5f, 0, 0), .5f, center, radii));
            Assert.IsFalse(ShieldMath.SphereOverlaps(center + new float3(10, 0, 0), .5f, center, radii));
            Assert.IsTrue(ShieldMath.SphereOverlaps(center + new float3(5, 0, 0), .5f, center, radii),
                "The minimum-radius expansion permits a conservative false positive along the long axis.");
            Assert.IsTrue(ShieldMath.SphereOverlaps(new float3(float.MaxValue, 0, 0), float.MaxValue, 0, new float3(1e-30f)));
        }

        [Test]
        public void SegmentEnters_MatchesBruteForce()
        {
            var random = RngStreams.For(1, RngPurpose.Test, 0);
            int hitCount = 0;
            for (int sample = 0; sample < 10000; sample++)
            {
                var center = random.NextFloat3(-10, 10);
                var radii = random.NextFloat3(.5f, 4f);
                var direction = math.normalizesafe(random.NextFloat3(-1, 1), new float3(1, 0, 0));
                var axis = math.abs(direction.y) < .9f ? new float3(0, 1, 0) : new float3(1, 0, 0);
                var perpendicular = math.normalize(math.cross(direction, axis));
                float distance0 = random.NextFloat(2, 4), distance1 = random.NextFloat(2, 4);
                var offset = perpendicular * random.NextFloat(-.5f, .5f);
                float3 start, end;
                // Domains are selected independently of the analytic result, with no output-based rejection (Q12).
                switch (sample % 4)
                {
                    case 0:
                        start = -direction * distance0 + offset;
                        end = direction * distance1 + offset;
                        break;
                    case 1:
                        offset = perpendicular * random.NextFloat(1.25f, 2f);
                        start = -direction * distance0 + offset;
                        end = direction * distance1 + offset;
                        break;
                    case 2:
                        start = direction * random.NextFloat(0, .5f);
                        end = direction * distance1 + offset;
                        break;
                    default:
                        start = end = direction * distance0 + offset;
                        break;
                }
                var p0 = center + start * radii;
                var p1 = center + end * radii;
                bool expected = March(p0, p1, center, radii, out var expectedT);
                Assert.AreEqual(sample % 4 == 0, expected, "Independent domain, sample " + sample);
                bool actual = ShieldMath.SegmentEnters(p0, p1, center, radii, out var actualT);
                Assert.AreEqual(expected, actual, "Hit classification, sample " + sample);
                Assert.IsTrue(math.isfinite(actualT), "Finite fraction, sample " + sample);
                if (expected)
                {
                    hitCount++;
                    Assert.AreEqual(expectedT, actualT, 1e-3, "Entry fraction, sample " + sample);
                }
                else Assert.AreEqual(0f, actualT);
            }
            Assert.AreEqual(2500, hitCount);
        }

        static bool March(float3 p0, float3 p1, float3 center, float3 radii, out double t)
        {
            t = 0;
            if (ReferenceContains((double3)p0, center, radii)) return false;
            for (int step = 1; step <= 1000; step++)
            {
                double fraction = step / 1000d;
                var p = (double3)p0 + ((double3)p1 - (double3)p0) * fraction;
                if (!ReferenceContains(p, center, radii)) continue;
                t = fraction;
                return true;
            }
            return false;
        }

        static bool ReferenceContains(double3 p, float3 center, float3 radii)
        {
            // The oracle uses independent scalar occupancy samples, never ShieldMath.Contains or its quadratic.
            double x = (p.x - center.x) / radii.x;
            double y = (p.y - center.y) / radii.y;
            double z = (p.z - center.z) / radii.z;
            return x * x + y * y + z * z <= 1;
        }
    }
}
