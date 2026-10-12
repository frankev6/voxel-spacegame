using Game.Presentation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>CAM-003 half-life, frame-rate and orbit regressions.</summary>
    public sealed class CameraMathTests
    {
        [Test] public void Damp_OneHalfLife_HalfWay()
        {
            Assert.AreEqual(5, CameraMath.Damp(2, 8, .12f, .12f), 1e-6f);
            float3 actual = CameraMath.Damp(new float3(2, 4, -6), new float3(8, -2, 10), .12f, .12f);
            Assert.That(math.distance(actual, new float3(5, 1, 2)), Is.LessThan(1e-6f));
        }

        [Test] public void Damp_FrameRateIndependent_30_60_144()
        {
            float expected = CameraMath.Damp(-12, 40, .35f, 1);
            float3 start = new float3(-12, 15, -40), target = new float3(40, -8, 27);
            float3 expectedVector = CameraMath.Damp(start, target, .35f, 1);
            foreach (int fps in new[] { 30, 60, 144 })
            {
                float scalar = -12; float3 vector = start;
                for (int frame = 0; frame < fps; frame++)
                {
                    scalar = CameraMath.Damp(scalar, 40, .35f, 1f / fps);
                    vector = CameraMath.Damp(vector, target, .35f, 1f / fps);
                }
                Assert.AreEqual(expected, scalar, 1e-4f, "FPS " + fps);
                Assert.That(math.distance(expectedVector, vector), Is.LessThan(1e-4f), "FPS " + fps);
            }
        }

        [Test] public void DampAngle_ShortestArc()
        {
            Assert.AreEqual(180, CameraMath.DampAngle(170, -170, .12f, .12f), 1e-5f);
            Assert.AreEqual(-175, CameraMath.DampAngle(180, -170, .12f, .12f), 1e-5f);
            Assert.AreEqual(180, CameraMath.DampAngle(-170, 170, .12f, .12f), 1e-5f);
        }

        [Test] public void OrbitRotation_PositivePitch_LooksDown()
        {
            foreach (float yaw in new[] { 0f, 90f })
            {
                var rotation = CameraMath.OrbitRotation(55, yaw);
                var forward = math.rotate(rotation, new float3(0, 0, 1));
                Assert.Less(forward.y, 0);
                Assert.AreEqual(55, math.degrees(math.asin(-forward.y)), 1e-4f);
                var focus = new float3(4, 0, 8);
                var position = CameraMath.OrbitPosition(focus, rotation, 150);
                Assert.Greater(position.y, focus.y);
                Assert.That(math.distance(math.normalize(focus - position), forward), Is.LessThan(1e-6f));
                Assert.AreEqual(150, math.distance(position, focus), 1e-4f);
            }
        }

        [Test] public void HeadingYaw_PlusZ_IsZero()
            => Assert.AreEqual(0, CameraMath.HeadingYawDeg(quaternion.identity));
    }
}
