using System.Diagnostics;
using Game.App.Boot;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Tests.Common.Perf;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;

namespace Game.Tests.PlayMode.Voxel
{
    /// <summary>SPEC-10 §8: 1024 dense brick creation/copy on the main-thread init boundary, median of twenty.</summary>
    public sealed class GridSpawnPerfTests
    {
        [Test, Performance, Category("Performance")]
        public void Create1024Bricks_Under6ms()
        {
            using var content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
            var voxelContent = new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations };
            using var library = new BlueprintLibrary(content.Catalog, voxelContent);
            using var source = new VoxelStructure();
            ushort block = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
            ushort palette = source.FindOrAddPalette(new PaletteEntry { Block = block });
            var cells = new NativeArray<Cell>(4096, Allocator.Temp);
            try
            {
                // Dense, nonuniform pages: all 4096 cells are copied, rather than the uniform expansion shortcut.
                for (int i = 0; i < cells.Length; i++) cells[i] = i == 0 ? default : Cell.Palette(palette);
                for (int i = 0; i < 1024; i++) source.SetBrickDense(new int3(i & 31, 0, i >> 5), cells);
            }
            finally { cells.Dispose(); }
            source.GetMass(voxelContent); // Spawn consumes the revision cache; mass computation has its separate gate.
            var registry = VoxelStructureRegistry.CreateRuntime(library);
            try
            {
                using var world = new World("GridSpawnPerfTests"); var em = world.EntityManager;
                content.CreateSingletons(em); em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024 }); em.CreateEntity(typeof(GameplayActive));
                var system = world.CreateSystem<GridSpawnSystem>(); int handle = registry.Register(source);
                double Sample()
                {
                    var root = GridSpawnApi.RequestSpawn(em, registry, handle, new GridSpawnParams
                    { PersistentId = 1, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender });
                    long start = Stopwatch.GetTimestamp(); system.Update(world.Unmanaged);
                    double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    Assert.AreEqual(1025, em.GetBuffer<LinkedEntityGroup>(root).Length);
                    Assert.IsFalse(source.IsPinned); em.DestroyEntity(root); return ms;
                }
                for (int i = 0; i < 5; i++) Sample();
                var samples = new double[20]; for (int i = 0; i < samples.Length; i++) samples[i] = Sample();
                var stat = PerfRun.Gate("Create1024Bricks", samples, 6);
                TestContext.WriteLine($"1024 dense bricks: median {stat.Median:F3} ms, p95 {stat.P95:F3} ms");
            }
            finally { Object.DestroyImmediate(registry); }
        }
    }
}
