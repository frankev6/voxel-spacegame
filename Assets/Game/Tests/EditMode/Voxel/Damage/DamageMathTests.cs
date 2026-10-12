using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public sealed class DamageMathTests
    {
        [Test] public void Quantize_IsUnbiased()
        {
            double loss = 0;
            for (int i = 0; i < 10000; i++)
            {
                byte hp = DamageMath.QuantizeHp(100f - .17f, 100f, DamageMath.Dither01(19, 23, new int3(-17, 3, 20), i));
                loss += 100f - DamageMath.HpUnits(hp, 100f);
            }
            Assert.That(loss / 10000, Is.EqualTo(.17).Within(.0017));
            Assert.AreEqual(new int3(-1, -2, 3), DamageMath.CellOf(new float3(-.1f, -.9f, 1.5f), .5f));
        }
        [Test] public void BlastFalloff_Endpoints()
        {
            Assert.AreEqual(1f, DamageMath.BlastFalloff(0, 3)); Assert.AreEqual(.25f, DamageMath.BlastFalloff(1.5f, 3));
            Assert.AreEqual(0f, DamageMath.BlastFalloff(3, 3)); Assert.AreEqual(0f, DamageMath.BlastFalloff(4, 3));
        }
        [Test] public void VelocityAtPoint_MatchesRigidMotion()
        {
            var t = LocalTransform.FromPositionRotation(new float3(3, 4, 5), quaternion.EulerXYZ(.2f, .7f, -.4f));
            var m = new PhysicsMass { Transform = new RigidTransform(quaternion.EulerXYZ(.3f, -.5f, 1f), new float3(1, 2, 3)) };
            var v = new PhysicsVelocity { Linear = new float3(4, 5, 6), Angular = new float3(2, 3, 4) };
            float3 point = new float3(-1, 3, 9);
            float3 expected = v.Linear + math.cross(math.rotate(t.Rotation, math.rotate(m.InertiaOrientation, v.Angular)), point - t.TransformPoint(m.CenterOfMass));
            Assert.Less(math.distance(expected, DamageMath.VelocityAtPoint(v, m, t, point)), 1e-4f);
        }
        [Test] public void RamEnergy_MatchesFormula()
        {
            Assert.That(DamageMath.RamEnergy(89000f, 1f / 25000, 1f / 3000, 1.21f), Is.EqualTo(1221972.45f).Within(2f));
            Assert.AreEqual(0, DamageMath.RamEnergy(100, 0, 0, 1.21f));
        }
    }
}
