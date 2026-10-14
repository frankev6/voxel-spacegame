using Game.ShipEditor.Services;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class DraftPickerTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static Ray Ray(float3 origin, float3 direction) => new((Vector3)origin, (Vector3)direction);
        static void Hit(DraftPicker.Hit hit, DraftPicker.HitKind kind, int3 cell, int3 normal, float distance)
        {
            Assert.AreEqual(kind, hit.Kind); Assert.AreEqual(cell, hit.Cell); Assert.AreEqual(normal, hit.Normal);
            Assert.AreEqual(distance, hit.Distance, .0001f);
            Assert.AreEqual(kind == DraftPicker.HitKind.Block ? cell + normal : cell, hit.PlacementCell);
        }
        [Test] public void HitCellAndNormal_SixDirections()
        {
            using var f = new DraftAdapterTests.Fixture(); int3 cell = new(-2, -3, -4);
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = cell });
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int3 normal = default; normal[axis] = sign;
                    var ray = Ray((float3)cell + .5f + (float3)normal * 5, -(float3)normal * 7);
                    Hit(DraftPicker.Raycast(f.Draft, f.Context, ray), DraftPicker.HitKind.Block, cell, normal, 4.5f);
                }
        }
        [Test] public void LongShip_FarEndPickable()
        {
            // Two occupied cells define a 1,000-cell extent; the empty span needs no large ship fixture.
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(0, 1, 0) },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(999, 0, 0) });
            var ray = Ray(new float3(.5f), new float3(1, 0, 0));
            Hit(DraftPicker.Raycast(f.Draft, f.Context, ray), DraftPicker.HitKind.Block,
                new int3(999, 0, 0), new int3(-1, 0, 0), 998.5f);
            Hit(DraftPicker.Raycast(f.Draft, f.Context, Ray(new float3(6000, .5f, .5f), new float3(-1, 0, 0))),
                DraftPicker.HitKind.Block, new int3(999, 0, 0), new int3(1, 0, 0), 5000);
            f.Context.Config.MaxPickSteps = 4;
            Assert.AreEqual(DraftPicker.HitKind.None, DraftPicker.Raycast(f.Draft, f.Context, ray).Kind);
        }
        [Test] public void Slice_SkipsAbove()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Single, Cell = new int3(0, 3, 0) },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Single });
            var ray = Ray(new float3(.5f, 5, .5f), new float3(0, -1, 0));
            Hit(DraftPicker.Raycast(f.Draft, f.Context, ray), DraftPicker.HitKind.Block,
                new int3(0, 3, 0), new int3(0, 1, 0), 1);
            Hit(DraftPicker.Raycast(f.Draft, f.Context, ray, true, 1), DraftPicker.HitKind.Block,
                int3.zero, new int3(0, 1, 0), 4);
            Hit(DraftPicker.Raycast(f.Draft, f.Context, ray, true, -1), DraftPicker.HitKind.Plane,
                new int3(0, -1, 0), new int3(0, 1, 0), 6);
        }
        [Test] public void XRay_SkipsStructural()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(-1, 1, 0) },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi });
            var ray = Ray(new float3(-5, 1.5f, .5f), new float3(1, 0, 0));
            Hit(DraftPicker.Raycast(f.Draft, f.Context, ray), DraftPicker.HitKind.Block,
                new int3(-1, 1, 0), new int3(-1, 0, 0), 4);
            Hit(DraftPicker.Raycast(f.Draft, f.Context, ray, xRay: true), DraftPicker.HitKind.Block,
                new int3(0, 1, 0), new int3(-1, 0, 0), 5);
            Assert.IsTrue(f.Draft.TryGetBlockAt(new int3(0, 1, 0), out var placed, out _));
            Assert.AreEqual(int3.zero, placed.Origin, "Picking returns the actual hit cell of a multi-cell instance.");
        }
        [Test] public void Empty_HitsWorkingPlane()
        {
            using var f = new DraftAdapterTests.Fixture();
            var ray = Ray(new float3(.5f, 10, -.5f), new float3(0, -1, 0));
            Hit(DraftPicker.Raycast(f.Draft, f.Context, ray), DraftPicker.HitKind.Plane,
                new int3(0, 0, -1), new int3(0, 1, 0), 10);
            Hit(DraftPicker.Raycast(f.Draft, f.Context, ray, true, -2), DraftPicker.HitKind.Plane,
                new int3(0, -2, -1), new int3(0, 1, 0), 12);
            Assert.AreEqual(DraftPicker.HitKind.None,
                DraftPicker.Raycast(f.Draft, f.Context, Ray(new float3(0, 1, 0), new float3(1, 0, 0))).Kind);
            // Empty storage can retain a brick after a removal; it still uses y = 0.
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(0, -3, 0) },
                new VoxelEdit { Kind = VoxelEditKind.RemoveBlock, Block = f.Hull, Cell = new int3(0, -3, 0) });
            Hit(DraftPicker.Raycast(f.Draft, f.Context, ray), DraftPicker.HitKind.Plane,
                new int3(0, 0, -1), new int3(0, 1, 0), 10);
        }
        [Test] public void Miss_HitsWorkingPlane()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(0, 2, 0) });
            Hit(DraftPicker.Raycast(f.Draft, f.Context, Ray(new float3(4.5f, 10, -2.5f), new float3(0, -1, 0))),
                DraftPicker.HitKind.Plane, new int3(4, 2, -3), new int3(0, 1, 0), 8);
            Assert.AreEqual(DraftPicker.HitKind.None,
                DraftPicker.Raycast(f.Draft, f.Context, Ray(new float3(4.5f, 5000, -2.5f), new float3(0, -1, 0))).Kind);
            Assert.AreEqual(DraftPicker.HitKind.None,
                DraftPicker.Raycast(f.Draft, f.Context, Ray(new float3(4.5f, 0, -2.5f), new float3(0, -1, 0))).Kind);
        }
    }
}
