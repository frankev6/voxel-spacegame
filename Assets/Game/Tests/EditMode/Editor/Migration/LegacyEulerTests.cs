using Game.Data.Blocks;
using Game.Editor.Migration;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>SPEC-72 MIG-007: export vectors and all cube Euler combinations against Unity's independent implementation.</summary>
    public sealed class LegacyEulerTests
    {
        [Test] public void TestVectors()
        {
            for (int yaw = 0; yaw < 4; yaw++) for (int roll = 0; roll < 4; roll++)
            {
                Assert.IsTrue(LegacyEuler.TryToOrientation(0, yaw * 90, roll * 90, out byte orientation, out float error));
                Assert.AreEqual(yaw * 4 + roll, orientation); Assert.AreEqual(0, error);
            }
            foreach (float snapped in new[] { .99999988f, .70710671f })
            {
                Assert.IsTrue(LegacyEuler.TryToOrientation(0, snapped, 0, out byte orientation, out float error));
                Assert.AreEqual(0, orientation); Assert.AreEqual(snapped, error);
            }
            Assert.IsTrue(LegacyEuler.TryToOrientation(90, 0, 0, out byte up, out _)); Assert.AreEqual(20, up);
            Assert.IsTrue(LegacyEuler.TryToOrientation(270, 0, 0, out byte down, out _)); Assert.AreEqual(16, down);
            Assert.IsFalse(LegacyEuler.TryToOrientation(0, 45, 0, out byte invalid, out float deviation));
            Assert.AreEqual(Orientation.Invalid, invalid); Assert.AreEqual(45, deviation);
        }
        [Test] public void MatchesUnityQuaternionEuler_All64()
        {
            var axes = new[] { new int3(1, 0, 0), new int3(0, 1, 0), new int3(0, 0, 1) };
            for (int x = 0; x < 360; x += 90) for (int y = 0; y < 360; y += 90) for (int z = 0; z < 360; z += 90)
            {
                Assert.IsTrue(LegacyEuler.TryToOrientation(x, y, z, out byte orientation, out _));
                Quaternion q = Quaternion.Euler(x, y, z);
                foreach (int3 v in axes)
                {
                    Vector3 transformed = q * new Vector3(v.x, v.y, v.z);
                    var expected = new int3(Mathf.RoundToInt(transformed.x), Mathf.RoundToInt(transformed.y), Mathf.RoundToInt(transformed.z));
                    Assert.AreEqual(expected, OrientationTables.Rotate(v, orientation), $"Euler ({x},{y},{z}), axis {v}");
                }
            }
        }
        [Test] public void WrapToleranceAndNonfinite_ReturnDefinedResults()
        {
            foreach (float yaw in new[] { -90f, 270f, 630f, -450f })
            {
                Assert.IsTrue(LegacyEuler.TryToOrientation(0, yaw, 0, out byte o, out float error));
                Assert.AreEqual(12, o); Assert.AreEqual(0, error);
            }
            Assert.IsTrue(LegacyEuler.TryToOrientation(1, 89, -1, out byte snapped, out float max));
            Assert.AreEqual(4, snapped); Assert.AreEqual(1, max);
            Assert.IsFalse(LegacyEuler.TryToOrientation(0, 88.999f, 0, out _, out _));
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.IsFalse(LegacyEuler.TryToOrientation(value, 0, 0, out byte o, out float error));
                Assert.AreEqual(Orientation.Invalid, o); Assert.IsTrue(float.IsPositiveInfinity(error));
            }
            // The largest finite float must never overflow the quarter-turn integer cast.
            Assert.DoesNotThrow(() => LegacyEuler.TryToOrientation(float.MaxValue, 0, 0, out _, out _));
        }
    }
}
