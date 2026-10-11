using System.Collections.Generic;
using System.Diagnostics;
using Game.Tests.Common.Perf;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-017: benchmark geometry counts and the reference station construction gate.</summary>
    public sealed class BenchmarkStructuresTests
    {
        static void Counts(VoxelStructure structure, int cells, int bricks)
        {
            using (structure)
            {
                Assert.AreEqual(cells, structure.SolidCellCount); Assert.AreEqual(bricks, structure.BrickCount);
                Assert.AreEqual(2, structure.Data.Palette.Length); Assert.AreEqual(1, structure.Data.Palette[1].Block);
                Assert.AreEqual(0, structure.Data.Palette[1].Color);
            }
        }
        [Test]
        public void Counts_MatchSpec()
        {
            Counts(BenchmarkStructures.HollowStation1M(1), 1008128, 872);
            Counts(BenchmarkStructures.ThinShellStation1M(1), 1043464, 3784);
            Counts(BenchmarkStructures.Ship20k(1), 20000, 10);
            Counts(BenchmarkStructures.Lattice2500(1), 2500, 2500);
            int cells = 0; var bricks = new HashSet<int3>();
            for (int z = 0; z < 128; z++) for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                if ((x + 0.5 - 64) * (x + 0.5 - 64) + (y + 0.5 - 64) * (y + 0.5 - 64) + (z + 0.5 - 64) * (z + 0.5 - 64) <= 62 * 62)
                { cells++; bricks.Add(new int3(x / 16, y / 16, z / 16)); }
            Assert.AreEqual(998592, cells); Assert.AreEqual(408, bricks.Count);
            Counts(BenchmarkStructures.SolidAsteroid1M(1), cells, bricks.Count);
        }
        [Test, Performance, Category("Performance")]
        public void HollowStation_BuildTime()
        {
            for (int i = 0; i < 3; i++) { using var warmup = BenchmarkStructures.HollowStation1M(1); }
            var samples = new double[11];
            for (int i = 0; i < samples.Length; i++)
            {
                var timer = Stopwatch.StartNew();
                using var structure = BenchmarkStructures.HollowStation1M(1);
                samples[i] = timer.Elapsed.TotalMilliseconds;
                Assert.AreEqual(1008128, structure.SolidCellCount);
            }
            PerfRun.Gate("HollowStationBuild", samples, 300);
        }
    }
}
