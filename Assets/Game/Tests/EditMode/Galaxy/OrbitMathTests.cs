using Game.Data.Definitions;
using Game.Galaxy.Data;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>GAL-002: SPEC-30 §5.5.5 and task acceptance criteria.</summary>
    public sealed class OrbitMathTests
    {
        const double Tau = 2.0 * math.PI;

        [Test]
        public void Circular_PeriodReturnsToStart()
        {
            var orbit = new OrbitElements { Mode = OrbitMode.Circular, RadiusM = 1e5, PeriodS = 3600, PhaseRad = .73 };
            var expected = new double3(orbit.RadiusM * math.cos(orbit.PhaseRad), 0, orbit.RadiusM * math.sin(orbit.PhaseRad));
            AssertNear(expected, OrbitMath.LocalPosition(orbit, 0), 1e-6);
            AssertNear(expected, OrbitMath.LocalPosition(orbit, orbit.PeriodS), 1e-6);
            AssertNear(expected, OrbitMath.LocalPosition(orbit, -orbit.PeriodS), 1e-6);
            AssertNear(new double3(-expected.z, 0, expected.x), OrbitMath.LocalPosition(orbit, orbit.PeriodS / 4), 1e-6);
            // Circular ignores fields belonging only to elliptic orbits.
            orbit.Eccentricity = .3; orbit.ArgPeriapsisRad = 1.2;
            AssertNear(expected, OrbitMath.LocalPosition(orbit, 0), 1e-6);
        }

        [Test]
        public void Velocity_MatchesFiniteDifference()
        {
            var orbits = new[]
            {
                new OrbitElements { Mode = OrbitMode.Fixed, RadiusM = 1e5, PhaseRad = -.8 }, // PeriodS = 0 is valid for Fixed.
                new OrbitElements { Mode = OrbitMode.Circular, RadiusM = 1e5, PeriodS = 3600, PhaseRad = .4 },
                new OrbitElements { Mode = OrbitMode.Elliptic, RadiusM = 1e5, PeriodS = 3600, PhaseRad = .4, Eccentricity = .3, ArgPeriapsisRad = 1.1 },
                new OrbitElements { Mode = OrbitMode.Elliptic, RadiusM = 1e5, PeriodS = 3600, PhaseRad = -.7, Eccentricity = 0, ArgPeriapsisRad = -.6 }
            };
            const double h = .01;
            foreach (var orbit in orbits)
                for (int i = -1; i <= 100; i++)
                {
                    // Includes t = 0 and a wrap at t = PeriodS, with negative time on the other side.
                    double t = i * 36.0;
                    var difference = (OrbitMath.LocalPosition(orbit, t + h) - OrbitMath.LocalPosition(orbit, t - h)) / (2.0 * h);
                    var actual = OrbitMath.LocalVelocity(orbit, t);
                    AssertNear(difference, actual, 1e-3);
                    Assert.That(actual.y, Is.Zero);
                    if (orbit.Mode == OrbitMode.Fixed) Assert.That(actual, Is.EqualTo(double3.zero));
                }
            var circular = orbits[1];
            AssertNear(circular.RadiusM * Tau / circular.PeriodS * new double3(-math.sin(circular.PhaseRad), 0, math.cos(circular.PhaseRad)),
                OrbitMath.LocalVelocity(circular, 0), 1e-9);
        }

        [Test]
        public void Elliptic_DistanceWithinApsides()
        {
            var orbit = new OrbitElements { Mode = OrbitMode.Elliptic, RadiusM = 1e5, PeriodS = 3600, Eccentricity = .3, ArgPeriapsisRad = .9 };
            for (int i = 0; i < 100; i++)
            {
                var position = OrbitMath.LocalPosition(orbit, i * orbit.PeriodS / 100.0);
                Assert.That(position.y, Is.Zero);
                Assert.That(math.length(position), Is.InRange(70000.0 - 1e-6, 130000.0 + 1e-6));
            }
            AssertNear(new double3(70000 * math.cos(.9), 0, -70000 * math.sin(.9)), OrbitMath.LocalPosition(orbit, 0), 1e-6);
            AssertNear(new double3(-130000 * math.cos(.9), 0, 130000 * math.sin(.9)), OrbitMath.LocalPosition(orbit, 1800), 1e-6);
            // Six Newton steps must satisfy Kepler's equation across the orbit, including nonzero phase.
            orbit.ArgPeriapsisRad = 0; orbit.PhaseRad = .41;
            for (int i = 0; i < 100; i++)
            {
                double t = i * orbit.PeriodS / 100.0;
                var position = OrbitMath.LocalPosition(orbit, t);
                double eccentricAnomaly = math.atan2(position.z / (orbit.RadiusM * math.sqrt(1 - .3 * .3)), position.x / orbit.RadiusM + .3);
                double meanAnomaly = orbit.PhaseRad + Tau * t / orbit.PeriodS;
                double residual = eccentricAnomaly - .3 * math.sin(eccentricAnomaly) - meanAnomaly;
                residual -= Tau * math.round(residual / Tau);
                Assert.That(math.abs(residual), Is.LessThan(1e-12));
            }
        }

        [Test]
        public void LargeTime_Stable()
        {
            var orbit = new OrbitElements { Mode = OrbitMode.Circular, RadiusM = 1e5, PeriodS = 3600.125, PhaseRad = -.37 };
            foreach (double t in new[] { 1e9, -1e9, 1e9 + .125 })
            {
                double remainder = t % orbit.PeriodS;
                if (remainder < 0) remainder += orbit.PeriodS;
                double angle = orbit.PhaseRad + Tau * remainder / orbit.PeriodS;
                var expected = orbit.RadiusM * new double3(math.cos(angle), 0, math.sin(angle));
                var actual = OrbitMath.LocalPosition(orbit, t);
                Assert.That(math.all(math.isfinite(actual)), Is.True);
                AssertNear(expected, actual, 1e-3);
                foreach (var mode in new[] { OrbitMode.Fixed, OrbitMode.Circular, OrbitMode.Elliptic })
                {
                    var variant = orbit; variant.Mode = mode; variant.Eccentricity = .3; variant.ArgPeriapsisRad = 1.1;
                    Assert.That(math.all(math.isfinite(OrbitMath.LocalPosition(variant, t))), Is.True);
                    Assert.That(math.all(math.isfinite(OrbitMath.LocalVelocity(variant, t))), Is.True);
                }
            }
        }

        [Test]
        public void Moon_ComposesParent()
        {
            var body = new BodySpec
            {
                ParentOrbit = new OrbitElements { Mode = OrbitMode.Circular, RadiusM = 1e5, PeriodS = 3600 },
                Orbit = new OrbitElements { Mode = OrbitMode.Circular, RadiusM = 1000, PeriodS = 600, PhaseRad = math.PI / 2 }
            };
            AssertNear(new double3(1e5, 0, 1000), OrbitMath.Position(body, 0), 1e-6);
            AssertNear(new double3(-1000 * Tau / 600, 0, 1e5 * Tau / 3600), OrbitMath.Velocity(body, 0), 1e-6);
            const double h = .01;
            for (int i = 0; i < 100; i++)
            {
                double t = i * 36.0;
                AssertNear(OrbitMath.LocalPosition(body.ParentOrbit, t) + OrbitMath.LocalPosition(body.Orbit, t), OrbitMath.Position(body, t), 1e-6);
                AssertNear((OrbitMath.Position(body, t + h) - OrbitMath.Position(body, t - h)) / (2 * h), OrbitMath.Velocity(body, t), 1e-3);
            }
            // An anchored story spawn moves with its parent and keeps its fixed offset.
            body.Orbit = new OrbitElements { Mode = OrbitMode.Fixed, RadiusM = 400, PhaseRad = math.PI };
            AssertNear(new double3(1e5 - 400, 0, 0), OrbitMath.Position(body, 0), 1e-6);
            AssertNear(OrbitMath.LocalVelocity(body.ParentOrbit, 900), OrbitMath.Velocity(body, 900), 1e-6);
            // A root's default parent contributes zero without a period or identity check.
            body.ParentOrbit = default;
            AssertNear(OrbitMath.LocalPosition(body.Orbit, 0), OrbitMath.Position(body, 0), 1e-6);
            AssertNear(double3.zero, OrbitMath.Velocity(body, 0), 1e-6);
        }

        static void AssertNear(double3 expected, double3 actual, double tolerance)
            => Assert.That(math.distance(expected, actual), Is.LessThanOrEqualTo(tolerance), "expected " + expected + ", actual " + actual);
    }
}
