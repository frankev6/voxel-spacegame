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
    /// <summary>VOX-020: advisory main-thread place/remove plus mirrored preview cost on 100,000 hull blocks.</summary>
    public sealed class GridEditPerfTests
    {
        [Test, Performance, Category("Performance")]
        public void PlaceRemove_Under2ms()
        {
            using var content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
            var voxel = new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations };
            using var library = new BlueprintLibrary(content.Catalog, voxel);
            using var source = new VoxelStructure();
            ushort block = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
            ushort palette = source.FindOrAddPalette(new PaletteEntry { Block = block });
            for (int z = 0; z < 100; z++) for (int y = 0; y < 10; y++) for (int x = 0; x < 100; x++)
                source.SetCell(new int3(x, y, z), Cell.Palette(palette));
            var registry = VoxelStructureRegistry.CreateRuntime(library);
            try
            {
                using var world = new World("GridEditPerfTests"); var em = world.EntityManager;
                content.CreateSingletons(em); em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024 }); em.CreateEntity(typeof(GameplayActive));
                var system = world.CreateSystem<GridSpawnSystem>();
                var root = GridSpawnApi.RequestSpawn(em, registry, registry.Register(source), new GridSpawnParams
                { Pose = RigidTransform.identity, Flags = GridSpawnFlags.EditorPreview | GridSpawnFlags.NoRender });
                system.Update(world.Unmanaged); Assert.IsFalse(source.IsPinned);
                var at = new int3(50, 10, 50); using var changed = new NativeArray<int3>(1, Allocator.Temp); changed[0] = BrickMath.BrickOf(at);
                double Sample(bool place)
                {
                    long start = Stopwatch.GetTimestamp();
                    bool changedDraft = place ? source.TryPlaceBlock(new BlockPlacement { Block = block, Origin = at }, voxel) == PlaceResult.Ok
                        : source.TryRemoveBlockAt(at, voxel, out _);
                    GridSpawnApi.RequestEdit(em, root, changed); system.Update(world.Unmanaged);
                    double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    Assert.IsTrue(changedDraft); Assert.IsFalse(em.IsComponentEnabled<GridEditRequest>(root));
                    var grid = em.GetComponentData<VoxelGrid>(root);
                    Entity brick = em.GetBuffer<BrickSlot>(root)[BrickMath.SlotIndex(changed[0], grid.BrickMin, grid.BrickDims)].Brick;
                    var cell = BrickWrite.Get(em.GetBuffer<BrickCells>(brick), em.GetBuffer<BrickSparseCells>(brick), BrickMath.LocalIndex(BrickMath.LocalOf(at)));
                    Assert.AreEqual(!place, cell.IsAir); return ms;
                }
                for (int i = 0; i < 5; i++) { Sample(true); Sample(false); }
                var samples = new double[40];
                for (int i = 0; i < samples.Length; i++) samples[i] = Sample((i & 1) == 0);
                var stat = PerfRun.Gate("GridEditPlaceRemove100k", samples, 2);
                TestContext.WriteLine($"100k preview place/remove+edit: median {stat.Median:F4} ms, p95 {stat.P95:F4} ms");
            }
            finally { Object.DestroyImmediate(registry); }
        }
    }
}
