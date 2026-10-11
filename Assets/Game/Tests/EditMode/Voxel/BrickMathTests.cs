using Game.Data.Blocks;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-004: the shared cell/brick layout, negative coordinates and face order (SPEC-10 §4.2).</summary>
    public sealed class BrickMathTests
    {
        [BurstCompile(CompileSynchronously = true)]
        struct RoundTripJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int3> Cells;
            public NativeArray<int3> Results;
            public void Execute(int i) => Results[i] = BrickMath.CellOf(BrickMath.BrickOf(Cells[i]), BrickMath.LocalIndex(BrickMath.LocalOf(Cells[i])));
        }

        [Test]
        public void CellBrickLocal_RoundTrip_Negatives()
        {
            var input = new int3[10000];
            var random = new Random(0xA48112u);
            for (int i = 0; i < input.Length; i++) input[i] = random.NextInt3(-5000, 5001);
            using var cells = new NativeArray<int3>(input, Allocator.TempJob);
            using var results = new NativeArray<int3>(cells.Length, Allocator.TempJob);
            new RoundTripJob { Cells = cells, Results = results }.Schedule(cells.Length, 64).Complete();
            for (int i = 0; i < cells.Length; i++)
            {
                Assert.AreEqual(cells[i], results[i]);
                Assert.IsTrue(math.all(BrickMath.LocalOf(cells[i]) >= 0) && math.all(BrickMath.LocalOf(cells[i]) < 16));
            }
        }

        [Test]
        public void SlotIndex_OutsideReturnsMinusOne()
        {
            var min = new int3(-3, 7, -12); var dims = new int3(4, 2, 5);
            for (int z = -1; z <= dims.z; z++)
                for (int y = -1; y <= dims.y; y++)
                    for (int x = -1; x <= dims.x; x++)
                    {
                        bool inside = x >= 0 && x < dims.x && y >= 0 && y < dims.y && z >= 0 && z < dims.z;
                        Assert.AreEqual(inside ? x + 4 * (y + 2 * z) : -1, BrickMath.SlotIndex(min + new int3(x, y, z), min, dims));
                    }
        }

        [Test]
        public void BorderFaces_Corners()
        {
            for (int z = 0; z <= 15; z += 15)
                for (int y = 0; y <= 15; y += 15)
                    for (int x = 0; x <= 15; x += 15)
                    {
                        var expected = (x == 0 ? BlockFaceMask.NegX : BlockFaceMask.PosX)
                            | (y == 0 ? BlockFaceMask.NegY : BlockFaceMask.PosY)
                            | (z == 0 ? BlockFaceMask.NegZ : BlockFaceMask.PosZ);
                        Assert.AreEqual(expected, BrickMath.BorderFaces(BrickMath.LocalIndex(x, y, z)));
                    }
            Assert.AreEqual(BlockFaceMask.NegX | BlockFaceMask.PosY, BrickMath.BorderFaces(BrickMath.LocalIndex(0, 15, 7)));
            Assert.AreEqual(BlockFaceMask.None, BrickMath.BorderFaces(BrickMath.LocalIndex(3, 4, 5)));
            var directions = new[] { new int3(1, 0, 0), new int3(-1, 0, 0), new int3(0, 1, 0), new int3(0, -1, 0), new int3(0, 0, 1), new int3(0, 0, -1) };
            for (int i = 0; i < directions.Length; i++) Assert.AreEqual(directions[i], BrickMath.FaceDirection((BlockFace)i));
        }
    }
}
