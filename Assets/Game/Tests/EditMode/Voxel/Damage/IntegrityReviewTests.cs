using System.Collections.Generic;
using Game.App.Session;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Editor.Content;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public unsafe sealed class IntegrityReviewTests
    {
        static DamageStreamsData* Run(World world, SystemHandle system)
        {
            system.Update(world.Unmanaged); world.EntityManager.CompleteAllTrackedJobs();
            using var query = world.EntityManager.CreateEntityQuery(typeof(DamageStreams));
            var data = query.GetSingleton<DamageStreams>().Data; data->IntegrityHandle.Complete(); return data;
        }
        [Test] public void OverCap_DefersDeterministicallyAndRetainsSeed()
        {
            using var f = new TestGridFactory();
            var root = f.Spawn(f.Bridge()); f.World.CreateSystem<DamageStreamsSystem>();
            using var configQuery = f.EM.CreateEntityQuery(typeof(DamageConfig));
            var config = DamageConfig.Default; config.MaxFullFloodCells = 1; configQuery.SetSingleton(config);
            var seed = new int3(0); f.EM.GetBuffer<IntegritySeed>(root).Add(new IntegritySeed { Cell = seed });
            var system = f.World.CreateSystem<IntegritySystem>();
            LogAssert.Expect(LogType.Warning, "[Damage] flood deferred; seeds kept. cap=1");
            for (int tick = 0; tick < 3; tick++)
            {
                var data = Run(f.World, system);
                Assert.AreEqual(tick + 1, data->Current.IntegritySizeDeferred);
                Assert.AreEqual(0, data->Islands.Length); Assert.AreEqual(0, data->IslandMasks.Length);
                Assert.AreEqual(1, f.EM.GetBuffer<IntegritySeed>(root).Length);
                Assert.AreEqual(seed, f.EM.GetBuffer<IntegritySeed>(root)[0].Cell);
            }
            // Work is queued, not discarded: lifting the cap consumes the same seed.
            config.MaxFullFloodCells = DamageConfig.Default.MaxFullFloodCells; configQuery.SetSingleton(config);
            Run(f.World, system); Assert.AreEqual(0, f.EM.GetBuffer<IntegritySeed>(root).Length);
        }
        [Test] public void StationBreakaway_MasksMatchPreviousSlotScan()
        {
            using var content = new ContentService(ContentDatabaseBuilder.Rebuild());
            var voxels = new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations };
            var config = AssetDatabase.LoadAssetAtPath<FiringRangeConfigDefinition>("Assets/Game/Content/Config/config_firing_range.asset");
            using var station = FiringRangeService.BuildStation(config, content.Catalog, voxels);
            station.SetCell(new int3(-13, 0, 0), default);
            using var library = new BlueprintLibrary(content.Catalog, voxels);
            var registry = VoxelStructureRegistry.CreateRuntime(library);
            try
            {
                using var world = new World("Station integrity reference"); var em = world.EntityManager;
                content.CreateSingletons(em); em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = 1 }); em.CreateEntity(typeof(GameplayActive));
                var spawn = world.CreateSystem<GridSpawnSystem>(); world.CreateSystem<DamageStreamsSystem>();
                var root = GridSpawnApi.RequestSpawn(em, registry, registry.Register(station), new GridSpawnParams
                    { PersistentId = 1, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender, Kind = GridKind.Station, Motion = GridMotion.Static });
                spawn.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                em.GetBuffer<IntegritySeed>(root).Add(new IntegritySeed { Cell = new int3(-13, 0, 0) });
                var data = Run(world, world.CreateSystem<IntegritySystem>());
                Assert.AreEqual(1, data->Islands.Length); Assert.AreEqual(0, data->Current.IntegritySizeDeferred);
                var grid = em.GetComponentData<VoxelGrid>(root); var slots = em.GetBuffer<BrickSlot>(root);
                var cells = new List<int3>(); station.TryGetCellBounds(out var min, out var max);
                for (int z = min.z; z <= max.z; z++) for (int y = min.y; y <= max.y; y++) for (int x = min.x; x < -13; x++)
                { var p = new int3(x,y,z); if (!station.GetCell(p).IsAir) cells.Add(p); }
                int emitted = 0;
                // Previous component × slot × node output, for the known single-bridge port component.
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    var expected = new CellMask(); bool any = false;
                    foreach (var p in cells)
                    {
                        if (BrickMath.SlotIndex(BrickMath.BrickOf(p), grid.BrickMin, grid.BrickDims) != slot) continue;
                        int local = BrickMath.LocalIndex(BrickMath.LocalOf(p)); expected.Words[local >> 6] |= 1UL << (local & 63); any = true;
                    }
                    if (!any) continue;
                    var actual = data->IslandMasks[emitted++]; Assert.AreEqual(slot, actual.SlotIndex);
                    for (int word = 0; word < 64; word++) Assert.AreEqual(expected.Words[word], actual.Mask.Words[word]);
                }
                TestContext.WriteLine($"Station remaining cells={station.SolidCellCount}; detached cells={cells.Count}; masks={emitted}; cap={DamageConfig.Default.MaxFullFloodCells}");
                Assert.AreEqual(cells.Count, data->Islands[0].CellCount); Assert.AreEqual(emitted, data->IslandMasks.Length);
            }
            finally { Object.DestroyImmediate(registry); }
        }
    }
}
