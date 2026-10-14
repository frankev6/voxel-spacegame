using Game.ShipEditor.Services;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class PlacementMathTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        [Test] public void MultiCell_NegativeNormal_ExtendsAway()
        {
            using var f = new DraftAdapterTests.Fixture();
            foreach (byte orientation in new byte[] { 0, 4, 13 })
            {
                var footprint = f.Context.Footprints.Get(f.Multi, orientation);
                for (int axis = 0; axis < 3; axis++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        int3 normal = default; normal[axis] = sign;
                        int3 target = new int3(-3, -2, -1) + normal;
                        int3 origin = PlacementMath.OriginFor(target, normal, footprint.Min, footprint.Max);
                        int3 min = new(int.MaxValue), max = new(int.MinValue);
                        foreach (var cell in footprint.Cells) { min = math.min(min, origin + cell); max = math.max(max, origin + cell); }
                        for (int i = 0; i < 3; i++)
                            Assert.AreEqual(target[i], normal[i] < 0 ? max[i] : min[i], "The footprint extends away from the clicked face.");
                    }
            }
        }
    }
}
