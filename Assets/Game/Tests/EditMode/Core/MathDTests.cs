using Game.Core;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-010: double-precision frame helpers (SPEC-02 §4.10).</summary>
    public sealed class MathDTests
    {
        [Test]
        public void LocalRoundTrip_Within1mm()
        {
            var origins = new[] { new double3(0, 0, 0), new double3(1.0e9, -3.5e8, 2.25e9), new double3(-7.7e10, 12.5, 4.4e10) };
            var rng = new Random(12345u);
            foreach (var o in origins)
                for (int i = 0; i < 500; i++)
                {
                    var p = o + (double3)(rng.NextFloat3Direction() * rng.NextFloat(0f, 10000f));
                    var back = MathD.ToStarSystem(MathD.ToLocal(p, o), o);
                    Assert.LessOrEqual(math.sqrt(MathD.DistanceSq(p, back)), 0.001, $"origin {o}, point {p}");
                }
            Assert.AreEqual(25.0, MathD.DistanceSq(new double3(0, 0, 0), new double3(3, 4, 0)));
        }

        [Test]
        public void IsFinite_DetectsNaN()
        {
            Assert.IsTrue(MathD.IsFinite(new double3(1, 2, 3)));
            Assert.IsFalse(MathD.IsFinite(new double3(double.NaN, 0, 0)));
            Assert.IsFalse(MathD.IsFinite(new double3(0, double.PositiveInfinity, 0)));
            Assert.IsTrue(MathD.IsFinite(new float3(1, 2, 3)));
            Assert.IsFalse(MathD.IsFinite(new float3(0, 0, float.NaN)));
            Assert.IsFalse(MathD.IsFinite(new float3(float.NegativeInfinity, 0, 0)));
        }
    }
}
