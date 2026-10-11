using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel.Physics
{
    /// <summary>PHY-005 grid-local offsets, sorted child order and nested queries after copied child disposal.</summary>
    public sealed class GridColliderAssemblerTests
    {
        [BurstCompile(CompileSynchronously = true)]
        struct AssembleJob : IJob
        {
            [ReadOnly] public NativeArray<BrickChild> Children;
            public float Scale;
            public NativeReference<BlobAssetReference<Collider>> Result;
            public void Execute() => Result.Value = GridColliderAssembler.Assemble(Children, Scale);
        }
        static BlobAssetReference<Collider> Brick(float scale, bool nested)
        {
            var mask = new BrickMaskBits(); mask.Set(0, true);
            if (nested) mask.Set(3, true);
            var scratch = new NativeList<BrickBox>(Allocator.Temp);
            try { return BrickColliderBuilder.Build(mask, scale, 64, 0.1f, PhysicsLayers.Grid, PhysicsLayers.GridMaterial, ref scratch, out _); }
            finally { scratch.Dispose(); }
        }
        static void Check(int3[] coords, float scale, bool nested)
        {
            var children = new NativeArray<BrickChild>(coords.Length, Allocator.TempJob);
            var result = new NativeReference<BlobAssetReference<Collider>>(Allocator.TempJob);
            try
            {
                for (int i = 0; i < coords.Length; i++) children[i] = new BrickChild { Coord = coords[i], Blob = Brick(scale, nested), BoxCount = nested ? 2 : 1 };
                new AssembleJob { Children = children, Scale = scale, Result = result }.Schedule().Complete();
                for (int i = 0; i < children.Length; i++)
                {
                    Assert.IsTrue(children[i].Blob.IsCreated);
                    children[i].Blob.Dispose(); children[i] = default;
                }
                foreach (var coord in coords) for (int box = 0; box < (nested ? 2 : 1); box++)
                {
                    float3 offset = (float3)(coord * 16) * scale;
                    float3 centre = offset + new float3(box * 3 + 0.5f, 0.5f, 0.5f) * scale;
                    Assert.IsTrue(result.Value.Value.CastRay(new RaycastInput
                    {
                        Start = centre + new float3(0, 2 * scale, 0), End = centre - new float3(0, 2 * scale, 0), Filter = PhysicsLayers.ProjectileQuery,
                    }, out var hit));
                    Assert.That(math.distance(hit.Position, centre + new float3(0, 0.5f * scale, 0)), Is.LessThan(1e-4f));
                }
            }
            finally
            {
                if (result.Value.IsCreated) result.Value.Dispose(); result.Dispose();
                for (int i = 0; i < children.Length; i++) if (children[i].Blob.IsCreated) children[i].Blob.Dispose();
                children.Dispose();
            }
        }
        [Test] public void ChildOffsets_CellSize() => Check(new[] { new int3(0), new int3(2, 3, 4) }, 0.3f, false);
        [Test] public void NegativeCoords() => Check(new[] { new int3(-3, -2, -1), new int3(1, 2, 3) }, 2, false);
        [Test] public void NestedRaycast_HitsExpectedBrick() => Check(new[] { new int3(-2, -1, -3), new int3(3, 2, 1) }, 0.5f, true);
        [Test]
        public unsafe void SortedOrder()
        {
            var coords = new[] { new int3(3, 0, 1), new int3(0, -2, 0), new int3(-1, 0, 1), new int3(0, 0, -1) };
            System.Array.Sort(coords, GridColliderAssembler.CompareCoords);
            var expected = new[] { new int3(0, 0, -1), new int3(0, -2, 0), new int3(-1, 0, 1), new int3(3, 0, 1) };
            CollectionAssert.AreEqual(expected, coords);
            Assert.AreEqual(-1, GridColliderAssembler.CompareCoords(new int3(int.MinValue), new int3(int.MaxValue)));
            Assert.AreEqual(1, GridColliderAssembler.CompareCoords(new int3(int.MaxValue), new int3(int.MinValue)));
            Assert.AreEqual(0, GridColliderAssembler.CompareCoords(coords[0], coords[0]));
            var children = new NativeArray<BrickChild>(coords.Length, Allocator.Temp);
            var brick = Brick(1, false); BlobAssetReference<Collider> grid = default;
            try
            {
                for (int i = 0; i < children.Length; i++) children[i] = new BrickChild { Coord = coords[i], Blob = brick, BoxCount = 1 };
                grid = GridColliderAssembler.Assemble(children, 1);
                var compound = (CompoundCollider*)grid.GetUnsafePtr();
                Assert.AreEqual(coords.Length, compound->NumChildren);
                for (int i = 0; i < coords.Length; i++) Assert.AreEqual((float3)(coords[i] * 16), compound->Children[i].CompoundFromChild.pos);
            }
            finally { if (grid.IsCreated) grid.Dispose(); brick.Dispose(); children.Dispose(); }
        }
    }
}
