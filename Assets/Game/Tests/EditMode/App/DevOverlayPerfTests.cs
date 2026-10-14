using System.Diagnostics;
using Game.App.DevRuntime.Overlays;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Game.Voxel.Model;
using Unity.Collections;
namespace Game.Tests.EditMode.App
{
    public sealed class DevOverlayPerfTests : GameTestBase
    {
        [Test, Performance, Category("Performance")]
        public void Bricks1M()
        {
            var world = CreateWorld(); var em = world.EntityManager; var root = OverlayTests.Grid(em, 870);
            em.SetComponentData(root, new VoxelGrid { BrickDims = new int3(10, 9, 10), CellSize = 1 });
            using (var query = em.CreateEntityQuery(typeof(Brick)))
            using (var bricks = query.ToEntityArray(Allocator.Temp))
            {
                int remaining = 1000000;
                for (int i = 0; i < bricks.Length; i++)
                {
                    ushort cells = (ushort)math.min(1150, remaining); remaining -= cells;
                    em.SetComponentData(bricks[i], new Brick { Grid = root, Coord = new int3(i % 10, i / 10 % 9, i / 90), SolidCount = cells });
                }
                Assert.AreEqual(0, remaining);
            }
            using var overlay = new DevOverlayRenderer { Enabled = DevOverlayFlags.Bricks };
            for (int i = 0; i < 10; i++) overlay.Gather(world.World, float3.zero);
            var samples = new double[31];
            for (int i = 0; i < samples.Length; i++)
            {
                long start = Stopwatch.GetTimestamp(); overlay.Gather(world.World, float3.zero);
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            Assert.AreEqual(870 * 24, overlay.Lines.Vertices.Length);
            PerfRun.Gate("DevOverlayBricks1M", samples, 1.5);
        }
    }
}
