using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public sealed class PointDamageTests
    {
        [Test] public void Penetration_OverflowCarries()
        {
            using var f = new TestGridFactory(); var root = f.Spawn(f.Box(0, new int3(3, 0, 0)));
            var w = f.Writer(root);
            try
            {
                w.Event = new DamageEvent { LocalPoint = new float3(0, .25f, .25f), LocalDir = new float3(1,0,0), Amount = 250, Penetration = 3 };
                w.PointDamage(); Assert.AreEqual(2, w.Removed.Length);
                Assert.IsTrue(w.Access.TryGetCell(new int3(2,0,0), out var cell, out _, out _));
                Assert.That(DamageMath.HpUnits(cell.Hp, 100), Is.EqualTo(50).Within(100f / 255));
                Assert.That(w.Stats.DamageTaken, Is.EqualTo(250).Within(.001f));
            }
            finally { w.Dispose(); }
        }
        [Test] public void EntersThroughHole()
        {
            using var f = new TestGridFactory(); var source = f.Box(0, new int3(2,0,0)); source.SetCell(0, default);
            var root = f.Spawn(source); var w = f.Writer(root);
            try
            {
                w.Event = new DamageEvent { LocalPoint = new float3(0,.25f,.25f), LocalDir = new float3(1,0,0), Amount = 150, Penetration = 1 };
                w.PointDamage(); Assert.AreEqual(1, w.Removed.Length); Assert.AreEqual(new int3(1,0,0), w.Removed[0].Cell);
                Assert.IsTrue(w.Access.TryGetCell(new int3(2,0,0), out var cell, out _, out _)); Assert.AreEqual(255, cell.Hp);
            }
            finally { w.Dispose(); }
        }
        [Test] public void ZeroDirection_HitsSingleCell()
        {
            using var f = new TestGridFactory(); var root = f.Spawn(f.Box(0, new int3(2,0,0))); var w = f.Writer(root);
            try
            {
                w.Event = new DamageEvent { LocalPoint = new float3(.75f,.25f,.25f), Amount = 1000, Penetration = 10 };
                w.PointDamage(); Assert.AreEqual(1, w.Removed.Length); Assert.AreEqual(new int3(1,0,0), w.Removed[0].Cell);
                w.PointDamage(); Assert.AreEqual(1, w.Removed.Length, "An air hit does not walk without a direction");
            }
            finally { w.Dispose(); }
        }
    }
}
