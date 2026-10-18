using System.Collections.Generic;
using Game.App.DevRuntime;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using BoxCollider = Unity.Physics.BoxCollider;

namespace Game.Tests.EditMode.App
{
    /// <summary>DEV-024: the probe reads the colliders right: clearance outside and inside, the first thing on a line, ships ignored
    /// unless asked for, and reachability stops at walls and goes through gaps.</summary>
    public sealed class DevProbeTests
    {
        World world; PhysicsWorld physics; readonly List<BlobAssetReference<Collider>> boxes = new();

        [TearDown] public void TearDown()
        {
            physics.Dispose(); foreach (var b in boxes) b.Dispose(); boxes.Clear();
            world?.Dispose(); world = null;
        }

        DevProbe Build(bool ships, params (GridKind kind, float3 center, float3 size)[] bodies)
        {
            world = new World("probe"); var em = world.EntityManager;
            physics = new PhysicsWorld(bodies.Length, 0, 0);
            var statics = physics.StaticBodies;
            for (int i = 0; i < bodies.Length; i++)
            {
                var e = em.CreateEntity(); em.AddComponentData(e, new VoxelGrid { Kind = bodies[i].kind });
                var box = BoxCollider.Create(new BoxGeometry { Size = bodies[i].size, Orientation = quaternion.identity }, Game.Voxel.PhysicsBody.PhysicsLayers.Grid);
                boxes.Add(box);
                statics[i] = new RigidBody { Entity = e, Collider = box, WorldFromBody = new RigidTransform(quaternion.identity, bodies[i].center), Scale = 1 };
            }
            physics.CollisionWorld.BuildBroadphase(ref physics, 1f / 60, float3.zero);
            return new DevProbe(physics.CollisionWorld, em, ships);
        }

        [Test] public void Clearance_Ray_AndShipsIgnored()
        {
            var probe = Build(false, (GridKind.Station, new float3(20, 0, 0), new float3(10)), (GridKind.Ship, new float3(-8, 0, 0), new float3(4)));
            Assert.AreEqual(15, probe.Clearance(float3.zero, 100, out var near), .05f, "nearest rock 15 m away; the ship at 6 m does not count");
            Assert.AreNotEqual(Entity.Null, near);
            Assert.Less(probe.Clearance(new float3(20, 0, 0), 100, out _), -4.9f, "inside the box: negative, its depth");
            Assert.IsTrue(probe.Ray(float3.zero, new float3(40, 0, 0), out float d, out _));
            Assert.AreEqual(15, d, .05f);
            Assert.IsFalse(probe.Ray(float3.zero, new float3(-40, 0, 0), out _, out _), "only the ship is that way");
            physics.Dispose(); foreach (var b in boxes) b.Dispose(); boxes.Clear(); world.Dispose();
            var withShips = Build(true, (GridKind.Station, new float3(20, 0, 0), new float3(10)), (GridKind.Ship, new float3(-8, 0, 0), new float3(4)));
            Assert.AreEqual(6, withShips.Clearance(float3.zero, 100, out _), .05f, "ships=1 counts the ship");
        }

        [Test] public void FlatClearance_IgnoresFloorAndRoof()
        {
            // a tunnel: floor 24 m below, roof 16 m above, a wall 40 m to the east
            var probe = Build(false, (GridKind.Station, new float3(0, -28, 0), new float3(400, 8, 400)), (GridKind.Station, new float3(0, 20, 0), new float3(400, 8, 400)),
                (GridKind.Station, new float3(45, 0, 0), new float3(10, 30, 400)));
            Assert.AreEqual(16, probe.Clearance(float3.zero, 100, out _), .05f, "3D: the roof is nearest");
            Assert.AreEqual(40, probe.FlatClearance(float3.zero, 100, out _), .5f, "across the plane: the wall");
        }

        [Test] public void Flood_StopsAtWalls_PassesGaps()
        {
            // a wall across x = 50 from z = 0 to 100 m; the scan is 10 × 10 cells of 10 m from (0, 0)
            var probe = Build(false, (GridKind.Asteroid, new float3(50, 0, 50), new float3(4, 20, 100)));
            var clear = probe.Scan(float2.zero, 10, 10, 10, 20);
            var reached = DevProbe.Flood(clear, 10, 10, 0, 5);
            Assert.IsTrue(reached[9 * 10 + 0], "this side of the wall");
            Assert.IsFalse(reached[0 * 10 + 9], "the far side is cut off");
            TearDown();
            // the wall stops at z = 80, and a far wall from z = 105 closes the scan: a 25 m gap
            probe = Build(false, (GridKind.Asteroid, new float3(50, 0, 40), new float3(4, 20, 80)), (GridKind.Asteroid, new float3(50, 0, 110), new float3(100, 20, 10)));
            clear = probe.Scan(float2.zero, 10, 10, 10, 20);
            reached = DevProbe.Flood(clear, 10, 10, 0, 5);
            Assert.IsTrue(reached[0 * 10 + 9], "round the end of the wall");
            Assert.IsFalse(DevProbe.Flood(clear, 10, 10, 0, 12)[0 * 10 + 9], "too wide a ship for the gap");
        }
    }
}
