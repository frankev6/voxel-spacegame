using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public sealed class CellWriterTests
    {
        [BurstCompile] struct RemoveJob : IJob
        {
            public CellWriter Writer;
            public int3 Cell;
            public CellRemovedReason Reason;
            public void Execute() => Writer.RemoveCell(Cell, Reason);
        }
        [Test] public void RemoveCell_UpdatesMaskCountAndDirty()
        {
            using var f = new TestGridFactory();
            // Both dense and sparse representations use the same removal routine.
            foreach (int extent in new[] { 3, 15 })
            {
                var root = f.Spawn(f.Box(new int3(0), new int3(extent))); f.CleanDirty(root);
                using var w = f.Writer(root); var brick = w.Access.Slots[0].Brick;
                int before = f.EM.GetComponentData<Brick>(brick).SolidCount;
                new RemoveJob { Writer = w, Cell = new int3(1) }.Schedule().Complete();
                Assert.AreEqual(before - 1, f.EM.GetComponentData<Brick>(brick).SolidCount);
                int local = BrickMath.LocalIndex(new int3(1));
                Assert.AreEqual(0, f.EM.GetBuffer<BrickSolidMask>(brick)[local >> 6].Bits & (1UL << (local & 63)));
                Assert.AreEqual(BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider, f.EM.GetComponentData<BrickDirty>(brick).Flags);
                Assert.AreEqual(1, w.Removed.Length); Assert.AreEqual(1, w.Touched.Length);
                Assert.IsFalse(f.EM.IsComponentEnabled<BrickDirty>(brick), "Flatten owns enable bits");
            }
        }
        [Test] public void RemoveCell_FaceCell_DirtiesNeighbour()
        {
            using var f = new TestGridFactory(); var root = f.Spawn(f.Box(new int3(-1, 0, 0), new int3(1, 1, 1))); f.CleanDirty(root);
            using var w = f.Writer(root);
            new RemoveJob { Writer = w, Cell = new int3(0) }.Schedule().Complete();
            Assert.AreEqual(2, w.Touched.Length);
            Assert.AreEqual(BrickDirtyFlags.Mesh, f.EM.GetComponentData<BrickDirty>(w.Access.Slots[0].Brick).Flags);
        }
        [Test] public void RemoveCell_LastCell_QueuesBrickDestroy()
        {
            using var f = new TestGridFactory(); var root = f.Spawn(f.Box(new int3(-17, 0, 0), new int3(-17, 0, 0)));
            using var w = f.Writer(root); Entity brick = w.Access.Slots[0].Brick;
            new RemoveJob { Writer = w, Cell = new int3(-17, 0, 0) }.Schedule().Complete();
            Assert.AreEqual(Entity.Null, w.Access.Slots[0].Brick); Assert.AreEqual(brick, w.DestroyList[0]);
            Assert.AreEqual(0, f.EM.GetComponentData<Brick>(brick).SolidCount);
        }
        [Test] public void Kick_DirectionsPerReason()
        {
            using var f = new TestGridFactory();
            for (int scenario = 0; scenario < 6; scenario++)
            {
                var root = f.Spawn(f.Box(new int3(2, 0, 0), new int3(2, 0, 0)));
                var w = f.Writer(root);
                try
                {
                    w.Config.KickRandomFraction = 0;
                    w.Transform = LocalTransform.FromPositionRotation(new float3(2, 3, 4), quaternion.RotateY(.7f));
                    w.Velocity = new PhysicsVelocity { Linear = new float3(1, 2, 3), Angular = new float3(0, 2, 0) };
                    w.IslandCentroidWorld = w.Transform.Position + new float3(3, 0, 0);
                    w.Event = new DamageEvent { LocalDir = new float3(0, 0, 1), Radius = scenario == 1 ? 3f : 0f,
                        Flags = scenario == 5 ? DamageEventFlags.NoKick : 0 };
                    var reason = scenario == 2 ? CellRemovedReason.DetachedParticle : scenario == 3 ? CellRemovedReason.DetachedBody :
                        scenario == 4 ? CellRemovedReason.Dissolved : CellRemovedReason.Destroyed;
                    new RemoveJob { Writer = w, Cell = new int3(2, 0, 0), Reason = reason }.Schedule().Complete();
                    var removed = w.Removed[0];
                    Assert.Less(math.distance(removed.WorldVel, w.Velocity.Linear + math.cross(new float3(0, 2, 0), removed.WorldPos - w.Transform.Position)), 1e-4);
                    float3 expected = scenario == 0 ? -math.rotate(w.Transform.Rotation, w.Event.LocalDir) :
                        scenario == 2 ? new float3(1, 0, 0) : math.normalizesafe(removed.WorldPos - w.Transform.Position);
                    if (scenario == 3 || scenario == 5) Assert.AreEqual(float3.zero, removed.Kick);
                    else Assert.Greater(math.dot(math.normalize(removed.Kick), expected), .9999f);
                }
                finally { w.Dispose(); }
            }
        }
    }
}
