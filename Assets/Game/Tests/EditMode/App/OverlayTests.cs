using Game.App.DevRuntime;
using Game.App.DevRuntime.Overlays;
using Game.Tests.Common;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using BoxCollider = Unity.Physics.BoxCollider;
namespace Game.Tests.EditMode.App
{
    public sealed class OverlayTests : GameTestBase
    {
        internal static Entity Grid(EntityManager em, int count)
        {
            var grid = em.CreateEntity(typeof(VoxelGrid), typeof(LocalToWorld));
            em.SetComponentData(grid, new VoxelGrid { BrickDims = new int3(count, 1, 1), CellSize = 1 });
            em.SetComponentData(grid, new LocalToWorld { Value = float4x4.identity });
            for (int i = 0; i < count; i++)
            {
                var brick = em.CreateEntity(typeof(Brick), typeof(BrickDirty));
                em.SetComponentData(brick, new Brick { Grid = grid, Coord = new int3(i, 0, 0), SolidCount = 1 });
                em.SetComponentData(brick, new BrickDirty { Flags = i == 0 ? BrickDirtyFlags.Mesh : BrickDirtyFlags.Collider });
            }
            return grid;
        }
        [Test] public void BrickEdges_Count()
        {
            var world = CreateWorld(); Grid(world.EntityManager, 2);
            using var overlay = new DevOverlayRenderer { Enabled = DevOverlayFlags.Bricks };
            overlay.Gather(world.World, float3.zero);
            Assert.AreEqual(24, overlay.Lines.Vertices.Length / 2);
            Assert.IsFalse(overlay.Lines.Truncated);
            overlay.Enabled = DevOverlayFlags.Dirty; overlay.Gather(world.World, float3.zero);
            Assert.AreEqual(24, overlay.Lines.Vertices.Length / 2);
            Assert.AreEqual(new Color32(255,255,0,255), overlay.Lines.Vertices[0].Color);
            Assert.AreEqual(new Color32(255,0,0,255), overlay.Lines.Vertices[24].Color);
        }
        [Test] public void ColliderBoxes_MatchCompound()
        {
            var world = CreateWorld(); var em = world.EntityManager; var grid = Grid(em, 0);
            var box = BoxCollider.Create(new BoxGeometry { Center = float3.zero, Size = new float3(2), Orientation = quaternion.identity, BevelRadius = 0 });
            BlobAssetReference<Collider> inner = default, outer = default;
            try
            {
                using var childStorage = new NativeArray<CompoundCollider.ColliderBlobInstance>(2, Allocator.Temp);
                var children = childStorage;
                children[0] = new CompoundCollider.ColliderBlobInstance { Collider = box, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(2,0,0)) };
                children[1] = new CompoundCollider.ColliderBlobInstance { Collider = box, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(6,0,0)) };
                inner = CompoundCollider.Create(children);
                using var nestedStorage = new NativeArray<CompoundCollider.ColliderBlobInstance>(1, Allocator.Temp);
                var nested = nestedStorage;
                nested[0] = new CompoundCollider.ColliderBlobInstance { Collider = inner, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(0,10,0)) };
                outer = CompoundCollider.Create(nested);
                em.AddComponentData(grid, new PhysicsCollider { Value = outer });
                using var overlay = new DevOverlayRenderer { Enabled = DevOverlayFlags.Colliders };
                overlay.Gather(world.World, float3.zero);
                Assert.AreEqual(2, overlay.Lines.Vertices.Length / 24);
                Assert.AreEqual(new Color32(0,255,0,255), overlay.Lines.Vertices[0].Color);
                Assert.AreEqual(new float3(1,9,-1), overlay.Lines.Vertices[0].Position, "Nested child transforms are composed.");
                em.SetComponentData(grid, new PhysicsCollider { Value = box }); overlay.Gather(world.World, float3.zero);
                Assert.AreEqual(24, overlay.Lines.Vertices.Length); Assert.AreEqual(new Color32(0,255,255,255), overlay.Lines.Vertices[0].Color);
                em.RemoveComponent<PhysicsCollider>(grid);
            }
            finally { if (outer.IsCreated) outer.Dispose(); if (inner.IsCreated) inner.Dispose(); box.Dispose(); }
        }
        [Test] public void Dispose_NoLeak()
        {
            var world = CreateWorld(); Grid(world.EntityManager, 2);
            var host = DevRuntimeHost.Create(null); var overlay = host.Overlays;
            try
            {
                var result = host.Commands.Execute("overlay name=bricks", host.Context);
                Assert.AreEqual(DevResultKind.Ok, result.Kind);
                overlay.Gather(world.World, float3.zero); Assert.IsTrue(overlay.Lines.IsCreated);
            }
            finally { host.Dispose(); }
            Assert.IsTrue(overlay.IsDisposed); Assert.IsFalse(overlay.Lines.IsCreated); Assert.IsFalse(overlay.Lines.Overflow.IsCreated);
            overlay.Dispose();
        }
    }
}
