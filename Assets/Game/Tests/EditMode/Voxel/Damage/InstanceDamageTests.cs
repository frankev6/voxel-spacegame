using Game.Data.Combat;
using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public sealed class InstanceDamageTests
    {
        static void Hit(ref CellWriter w, int3 cell, float amount)
        {
            w.Event = new DamageEvent { LocalPoint = ((float3)cell + .5f) * w.Access.Definition.CellSize, Amount = amount };
            w.PointDamage();
        }
        [Test] public void HalfFootprintLost_DestroysInstance()
        {
            using var f = new TestGridFactory(); var s = f.Box(new int3(0,-3,0), new int3(0,-3,0));
            f.Place(s, "block_thruster_medium", 0); var root = f.Spawn(s); var w = f.Writer(root);
            try
            {
                Assert.IsTrue(w.Access.TryGetCell(0, out var first, out _, out _)); int index = first.InstanceIndex;
                var instance = w.Access.Instances[index]; Entity functional = instance.Functional;
                ref var def = ref w.Access.Blocks.Value.Blocks[instance.Block]; Assert.AreEqual(10, def.Cells.Length);
                for (int i = 0; i < 4; i++) Hit(ref w, def.Cells[i], 100);
                Assert.AreEqual(6, w.Access.Instances[index].CellsAlive); Assert.AreEqual(0, w.Stats.InstancesDestroyed);
                Hit(ref w, def.Cells[4], 100);
                Assert.AreEqual(10, w.Removed.Length); Assert.AreEqual(1, w.Stats.InstancesDestroyed);
                Assert.AreEqual(0, w.Access.Instances[index].CellsAlive);
                Assert.AreNotEqual(Entity.Null, functional); Assert.IsTrue(w.DestroyList.Contains(functional));
                Assert.IsTrue(f.EM.Exists(functional), "The destroy pass owns structural deletion");
            }
            finally { w.Dispose(); }
        }
        [Test] public void FunctionHpZero_DestroysInstance()
        {
            using var f = new TestGridFactory(); var s = f.Box(new int3(0,-3,0), new int3(0,-3,0));
            f.Place(s, "block_thruster_medium", 0); var root = f.Spawn(s); var w = f.Writer(root);
            try
            {
                w.Access.TryGetCell(0, out var cell, out _, out _); int index = cell.InstanceIndex;
                var instance = w.Access.Instances[index]; instance.FunctionHp = 40; w.Access.Instances[index] = instance;
                Hit(ref w, 0, 40); Assert.AreEqual(10, w.Removed.Length); Assert.AreEqual(1, w.Stats.InstancesDestroyed);
                Assert.AreEqual(0, w.Access.Instances[index].FunctionHp);
                Hit(ref w, 0, 100); Assert.AreEqual(1, w.Stats.InstancesDestroyed);
            }
            finally { w.Dispose(); }
        }
        [Test] public void Reactor_WritesExplosionRequest()
        {
            using var f = new TestGridFactory(); var s = f.Box(new int3(0,-3,0), new int3(0,-3,0));
            f.Place(s, "block_reactor_small", new int3(2,0,0)); var root = f.Spawn(s); var w = f.Writer(root);
            try
            {
                w.Explosions.BeginForEachIndex(0); Hit(ref w, new int3(2,0,0), 10000); Hit(ref w, new int3(2,0,0), 10000); w.Explosions.EndForEachIndex();
                var reader = f.LastExplosionStream.AsReader(); Assert.AreEqual(1, reader.BeginForEachIndex(0));
                var e = reader.Read<ExplosionRequest>(); reader.EndForEachIndex();
                Assert.AreEqual(4 * w.Access.Definition.CellSize, e.Radius); Assert.AreEqual(400, e.Amount);
                Assert.AreEqual(root, e.Source); Assert.AreEqual(root, e.Instigator); Assert.AreEqual(DamageType.Explosive, e.Type);
                Assert.AreEqual(new float3(1.25f,.25f,.25f), e.WorldPos);
            }
            finally { w.Dispose(); }
        }
        [Test] public void CoreDestroyed_SetsCoreLost()
        {
            using var f = new TestGridFactory(); var root = f.Spawn(f.CoreAndReactor()); var w = f.Writer(root);
            try
            {
                int anchor = w.Access.Definition.AnchorInstance; Assert.GreaterOrEqual(anchor, 0);
                var instance = w.Access.Instances[anchor]; instance.FunctionHp = 1; w.Access.Instances[anchor] = instance;
                Hit(ref w, instance.Origin, 1);
                Assert.IsTrue(w.CoreLost); Assert.AreEqual(-1, w.Access.Definition.AnchorInstance);
                Assert.AreEqual(((float3)instance.Origin + .5f) * w.Access.Definition.CellSize, w.CoreWorldPos);
                Assert.AreEqual(1, w.Stats.InstancesDestroyed);
            }
            finally { w.Dispose(); }
        }
    }
}
