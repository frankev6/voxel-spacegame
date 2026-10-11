using System;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-007: independent double-precision sums, revision cache invalidation and repeatability.</summary>
    public sealed class StructureMassTests
    {
        static VoxelStructure Build(VoxelTestContent fixture, float size)
        {
            var result = new VoxelStructure(size); var content = fixture.Content; var rng = new Random(151);
            var types = new[] { fixture.Index("block_hull"), fixture.Index("block_hull_angled"), fixture.Index("block_core_cross"), fixture.Index("block_capacitor_small") };
            for (int i = 0; i < 300; i++) result.TryPlaceBlock(new BlockPlacement
            { Block = types[rng.NextInt(types.Length)], Origin = rng.NextInt3(-8, 9) * 4, Orient = (byte)rng.NextInt(24) }, content);
            ushort palette = result.FindOrAddPalette(new PaletteEntry { Block = types[1] });
            result.SetBrickUniform(new int3(-4, 3, 2), Cell.Palette(palette));
            return result;
        }
        static StructureMassData Brute(VoxelStructure structure, in VoxelContent content)
        {
            var result = new StructureMassData(); var data = structure.Data;
            for (int b = 0; b < data.Bricks.Length; b++) for (int i = 0; i < 4096; i++)
            {
                int3 cell = BrickMath.CellOf(data.Bricks[b].Coord, i); var value = structure.GetCell(cell);
                if (value.IsAir) continue;
                ushort block = value.IsInstance ? data.Instances[value.InstanceIndex].Block : data.Palette[value.PaletteIndex].Block;
                double mass = content.Blocks.Value.Blocks[block].CellMass;
                double x = (cell.x + 0.5) * structure.CellSize, y = (cell.y + 0.5) * structure.CellSize, z = (cell.z + 0.5) * structure.CellSize;
                result.Mass += mass; result.FirstMoment += new double3(mass * x, mass * y, mass * z);
                result.SecondDiag += new double3(mass * x * x, mass * y * y, mass * z * z);
                result.SecondOff += new double3(mass * x * y, mass * y * z, mass * z * x);
                result.CellInertia += mass * structure.CellSize * structure.CellSize / 6; result.CellCount++;
            }
            return result;
        }
        static double[] Values(StructureMassData m) => new[]
        { m.Mass, m.FirstMoment.x, m.FirstMoment.y, m.FirstMoment.z, m.SecondDiag.x, m.SecondDiag.y, m.SecondDiag.z, m.SecondOff.x, m.SecondOff.y, m.SecondOff.z, m.CellInertia };
        static void Close(StructureMassData expected, StructureMassData actual)
        {
            Assert.AreEqual(expected.CellCount, actual.CellCount);
            double[] e = Values(expected), a = Values(actual);
            for (int i = 0; i < e.Length; i++) Assert.That(a[i], Is.EqualTo(e[i]).Within(math.max(1, math.abs(e[i])) * 1e-9));
        }
        [Test]
        public void Mass_MatchesBruteForce()
        {
            using var fixture = new VoxelTestContent(); var content = fixture.Content;
            foreach (float size in new[] { 0.3f, 0.5f, 2f })
            {
                using var structure = Build(fixture, size);
                Close(Brute(structure, content), structure.GetMass(content));
                Assert.AreEqual(structure.Revision, structure.Data.Header.Value.MassRevision);
                int3 removed = new int3(-64, 48, 32);
                var before = structure.GetMass(content); var expected = before;
                MassAccumulator.RemoveCell(ref expected, removed, size, 15);
                structure.SetCell(removed, default);
                Close(expected, structure.GetMass(content)); Close(Brute(structure, content), structure.GetMass(content));
                var contribution = new StructureMassData(); MassAccumulator.AddCell(ref contribution, removed, size, 15);
                MassAccumulator.Add(ref expected, contribution); Close(before, expected);
            }
        }
        [Test]
        public void Mass_IsDeterministic()
        {
            using var fixture = new VoxelTestContent(); var content = fixture.Content;
            using var first = Build(fixture, 0.3f); using var second = first.Clone(); using var third = first.Clone();
            var sums = new[] { first.GetMass(content), second.GetMass(content), third.GetMass(content) };
            double[] reference = Values(sums[0]);
            for (int run = 1; run < sums.Length; run++)
            {
                Assert.AreEqual(sums[0].CellCount, sums[run].CellCount);
                double[] actual = Values(sums[run]);
                for (int i = 0; i < reference.Length; i++) Assert.AreEqual(BitConverter.DoubleToInt64Bits(reference[i]), BitConverter.DoubleToInt64Bits(actual[i]));
            }
            CollectionAssert.AreEqual(reference, Values(first.GetMass(content)));
        }
    }
}
