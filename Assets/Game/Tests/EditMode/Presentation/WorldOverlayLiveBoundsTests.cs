using Game.Presentation.Systems;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>HUD-022: a wreck's tag box follows the cells it has left, not the whole ship's spawn bounds.</summary>
    public sealed class WorldOverlayLiveBoundsTests
    {
        static StructureMassData Cells(int3 from, int3 to, float size = 1, float mass = 10)
        {
            var data = default(StructureMassData);
            for (int x = from.x; x <= to.x; x++) for (int y = from.y; y <= to.y; y++) for (int z = from.z; z <= to.z; z++)
                MassAccumulator.AddCell(ref data, new int3(x, y, z), size, mass);
            return data;
        }

        [Test] public void SolidBox_IsItsOwnBox()
        {
            // 4 × 2 × 6 cells of 0.5 m: the box is exact, before any clipping (spawn bounds far larger)
            var data = Cells(new int3(2, 0, -3), new int3(5, 1, 2), .5f);
            Assert.IsTrue(WorldOverlaySystem.LiveBounds(data, .5f, new float3(-100), new float3(100), out var min, out var max));
            Assert.That(math.distance(new float3(1, 0, -1.5f), min), Is.LessThan(1e-4f));
            Assert.That(math.distance(new float3(3, 1, 1.5f), max), Is.LessThan(1e-4f));
        }

        [Test] public void Remnant_TagsOnlyWhatIsLeft()
        {
            // a 40 m ship of which a 2 m block near one corner is left
            var data = Cells(new int3(30), new int3(31));
            Assert.IsTrue(WorldOverlaySystem.LiveBounds(data, 1, float3.zero, new float3(40), out var min, out var max));
            Assert.That(math.distance(new float3(30), min), Is.LessThan(1e-4f));
            Assert.That(math.distance(new float3(32), max), Is.LessThan(1e-4f));
        }

        [Test] public void TwoPieces_CoverBoth_WithinSpawnBounds()
        {
            var data = Cells(new int3(0), new int3(1));
            var far = Cells(new int3(10, 0, 0), new int3(11, 1, 1));
            data.Mass += far.Mass; data.FirstMoment += far.FirstMoment; data.SecondDiag += far.SecondDiag; data.CellCount += far.CellCount;
            Assert.IsTrue(WorldOverlaySystem.LiveBounds(data, 1, float3.zero, new float3(12, 2, 2), out var min, out var max));
            Assert.That(min.x, Is.LessThanOrEqualTo(.5f)); Assert.That(max.x, Is.GreaterThanOrEqualTo(11.5f));
            Assert.That(math.all(min >= 0) && math.all(max <= new float3(12, 2, 2)), Is.True, "clipped to the spawn bounds");
        }

        [Test] public void NoCells_NoTag()
        {
            Assert.IsFalse(WorldOverlaySystem.LiveBounds(default, 1, float3.zero, new float3(10), out var min, out var max));
            Assert.AreEqual(float3.zero, min); Assert.AreEqual(new float3(10), max);
        }
    }
}
