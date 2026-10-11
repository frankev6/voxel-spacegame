using Game.Core;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-010: Planar conventions (SPEC-02 §4.10, §5.10).</summary>
    public sealed class PlanarTests
    {
        static void AreClose(float3 expected, float3 actual, float tol, string msg = null)
        {
            Assert.AreEqual(expected.x, actual.x, tol, msg); Assert.AreEqual(expected.y, actual.y, tol, msg); Assert.AreEqual(expected.z, actual.z, tol, msg);
        }

        [Test]
        public void Yaw_Conventions()
        {
            Assert.AreEqual(math.PI / 2f, Planar.YawFromForward(new float3(1, 0, 0)));
            Assert.AreEqual(0f, Planar.YawFromForward(new float3(0, 0, 1)));
            Assert.AreEqual(0f, Planar.YawFromForward(new float3(0, 5, 0)), "vertical forward has no yaw");
            AreClose(new float3(1, 0, 0), Planar.ForwardFromYaw(math.PI / 2f), 1e-6f);
            for (int i = 0; i < 100; i++)
            {
                float yaw = -math.PI + i * (2f * math.PI / 100f);
                AreClose(Planar.ForwardFromYaw(yaw), math.mul(Planar.YawRotation(yaw), new float3(0, 0, 1)), 1e-5f, $"yaw {yaw}");
                Assert.AreEqual(0f, Planar.WrapAngle(Planar.YawFromForward(Planar.ForwardFromYaw(yaw)) - yaw + 1e-3f) - 1e-3f, 1e-5f, $"round trip {yaw} (±π are the same heading)");
            }
            Assert.AreEqual(new float2(1, 3), Planar.XZ(new float3(1, 2, 3)));
            Assert.AreEqual(new float3(1, 7, 3), Planar.FromXZ(new float2(1, 3), 7));
            Assert.AreEqual(new float3(1, 0, 3), Planar.FlattenY(new float3(1, 2, 3)));
        }

        [Test]
        public void SignedAngle_Clockwise_Positive()
        {
            Assert.AreEqual(math.PI / 2f, Planar.SignedAngleXZ(new float3(0, 0, 1), new float3(1, 0, 0)));
            Assert.AreEqual(-math.PI / 2f, Planar.SignedAngleXZ(new float3(0, 0, 1), new float3(-1, 0, 0)), 1e-6f);
            Assert.AreEqual(0f, Planar.SignedAngleXZ(new float3(0, 0, 1), new float3(0, 3, 2)), 1e-6f, "Y is ignored");
            Assert.AreEqual(0f, Planar.SignedAngleXZ(float3.zero, new float3(1, 0, 0)), "degenerate input");
        }

        [Test]
        public void WrapAngle_Range()
        {
            Assert.AreEqual(-math.PI, Planar.WrapAngle(math.PI));
            Assert.AreEqual(-math.PI, Planar.WrapAngle(3f * math.PI), 1e-6f);
            Assert.AreEqual(0f, Planar.WrapAngle(0f));
            Assert.AreEqual(0.5f, Planar.WrapAngle(0.5f + 4f * math.PI), 1e-5f);
            for (int i = -50; i <= 50; i++)
            {
                float w = Planar.WrapAngle(i * 0.73f);
                Assert.GreaterOrEqual(w, -math.PI - 1e-6f);
                Assert.Less(w, math.PI);
            }
        }

        [Test]
        public void RayPlaneY_HitAndMiss()
        {
            Assert.IsTrue(Planar.RayPlaneY(new float3(2, 10, 3), new float3(0, -1, 0), 0f, out var hit, out var d));
            Assert.AreEqual(new float3(2, 0, 3), hit);
            Assert.AreEqual(10f, d);
            Assert.IsTrue(Planar.RayPlaneY(new float3(0, 10, 0), new float3(0, -2, 0), 0f, out _, out d));
            Assert.AreEqual(10f, d, 1e-5f, "distance is in world units, not in t");
            Assert.IsFalse(Planar.RayPlaneY(new float3(2, 10, 3), new float3(0, 1, 0), 0f, out _, out _), "upward ray");
            Assert.IsFalse(Planar.RayPlaneY(new float3(2, 10, 3), new float3(1, 0, 0), 0f, out _, out _), "parallel ray");
        }
    }
}
