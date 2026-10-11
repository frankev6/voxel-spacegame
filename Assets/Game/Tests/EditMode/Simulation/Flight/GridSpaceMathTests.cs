using Game.Core;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation.Common;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>FLT-002 grid geometry against SPEC-20 §4.5 and the shipped SPEC-04 block frame.</summary>
    public sealed class GridSpaceMathTests
    {
        ContentService content;
        [SetUp] public void SetUp() => content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
        [TearDown] public void TearDown() => content?.Dispose();

        [Test] public void InstanceCenter_Examples()
        {
            ushort index = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_thruster_small")));
            ref var block = ref content.BlockDefs.Value.Blocks[index];
            ref var orientations = ref content.Orientations.Value;
            int3 origin = new int3(4, 0, 7);
            Assert.AreEqual(new float3(2.25f, .25f, 4), GridSpaceMath.InstanceCenter(ref block, ref orientations, 0, origin, .5f));
            Assert.AreEqual(new float3(2.25f, .25f, 3.5f), GridSpaceMath.InstanceCenter(ref block, ref orientations, 8, origin, .5f));
            Assert.AreEqual(new float3(2.25f, .25f, 3.75f), GridSpaceMath.CellCenter(origin, .5f));
            Assert.AreEqual(new float3(0, 0, 1), GridSpaceMath.BlockDirToGrid(new float3(0, 0, -1), ref orientations, 8));
        }

        [Test] public void BlockDirToGrid_AllOrientations_Orthonormal()
        {
            ref var orientations = ref content.Orientations.Value;
            for (byte orientation = 0; orientation < 24; orientation++)
            {
                float3 x = GridSpaceMath.BlockDirToGrid(new float3(1, 0, 0), ref orientations, orientation);
                float3 y = GridSpaceMath.BlockDirToGrid(new float3(0, 1, 0), ref orientations, orientation);
                float3 z = GridSpaceMath.BlockDirToGrid(new float3(0, 0, 1), ref orientations, orientation);
                Assert.AreEqual(1, math.lengthsq(x)); Assert.AreEqual(1, math.lengthsq(y)); Assert.AreEqual(1, math.lengthsq(z));
                Assert.AreEqual(0, math.dot(x, y)); Assert.AreEqual(0, math.dot(x, z)); Assert.AreEqual(0, math.dot(y, z));
                Assert.AreEqual(z, math.cross(x, y));
            }
        }

        [Test] public void WorldGrid_RoundTrip()
        {
            ref var orientations = ref content.Orientations.Value;
            float3 point = new float3(3.25f, -2.5f, 8.75f), direction = math.normalize(new float3(1, 2, -3));
            for (byte orientation = 0; orientation < 24; orientation++)
            {
                var root = LocalTransform.FromPositionRotationScale(new float3(-12, 3, 7), orientations.Rotations[orientation], 1.5f);
                var actual = GridSpaceMath.WorldToGridPoint(root, GridSpaceMath.GridToWorldPoint(root, point));
                Assert.Less(math.distance(point, actual), .00002f);
                var actualDirection = GridSpaceMath.WorldToGridDir(root, GridSpaceMath.GridToWorldDir(root, direction));
                Assert.Less(math.distance(direction, actualDirection), .000002f);
                Assert.AreEqual(1, math.length(GridSpaceMath.GridToWorldDir(root, direction)), .000002f);
            }
        }
    }
}
