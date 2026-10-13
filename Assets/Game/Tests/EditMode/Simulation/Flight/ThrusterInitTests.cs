using System.Collections.Generic;
using Game.Core;
using Game.Data.Components;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Simulation.Commands;
using Game.Simulation.Common;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Tests.Common;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Data;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>FLT-006 real spawn/profile integration and change-filtered nozzle geometry (SPEC-20 §5.2).</summary>
    public sealed class ThrusterInitTests
    {
        VoxelTestContent content;
        BlueprintLibrary library;
        VoxelStructureRegistry registry;
        EcsTestWorld world;
        EntityManager EM => world.EntityManager;
        static ContentHash Blueprint => new ContentId("ship_thruster_init_test").Hash;

        [SetUp]
        public void SetUp()
        {
            content = new VoxelTestContent();
            var definition = TestContentFactory.Create<ShipBlueprintDefinition>("ship_thruster_init_test");
            using (var source = new VoxelStructure())
            {
                ref var orientations = ref content.Service.Orientations.Value;
                for (int i = 0; i < 4; i++)
                    Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement
                    {
                        Block = content.Index("block_thruster_medium"), Origin = new int3(4 + i * 12, 4, 8),
                        Orient = orientations.AxisTurn(1, i)
                    }, content.Content));
                definition.EditorSetPayload(VoxelStructureCodec.Encode(source,
                    VoxsBlockNames.FromCatalog(content.Service.Catalog), VoxsEncodeOptions.ForAsset),
                    source.SolidCellCount, new Vector3Int(0, 0, 0), new Vector3Int(48, 8, 16));
            }
            var definitions = new List<ContentDefinition> { definition };
            foreach (var item in content.Service.Catalog.All<ItemDefinition>()) definitions.Add(item);
            var report = new ValidationReport("FLT-006");
            var catalog = ContentCatalog.Build(definitions, report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            library = new BlueprintLibrary(catalog, content.Content);
            registry = VoxelStructureRegistry.CreateRuntime(library);
            world = EcsTestWorld.Create(typeof(GridSpawnSystem), typeof(ThrusterInitSystem));
            content.Service.CreateSingletons(EM);
            world.AddSingleton(new VoxelStructureRegistryRef { Value = registry });
            world.AddSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
            world.AddSingleton(PowerTuning.Defaults);
            world.AddGameplayActive();
        }

        [TearDown]
        public void TearDown()
        {
            world?.Dispose();
            if (registry != null) Object.DestroyImmediate(registry);
            library?.Dispose(); content?.Dispose();
        }

        Entity Spawn(GridProfile profile = GridProfile.PlayerShip) => GridSpawnApi.RequestSpawnBlueprint(EM, Blueprint,
            new GridSpawnParams { PersistentId = 1, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender,
                Motion = GridMotion.Dynamic, Profile = profile });
        void Frame() => world.StepFrame(EcsTestWorld.TickSeconds);

        [Test]
        public void Registered_ComponentsPresent()
        {
            for (GridProfile profile = GridProfile.None; profile <= GridProfile.Dummy; profile++)
            {
                var root = Spawn(profile); Frame();
                bool flying = profile != GridProfile.None && profile != GridProfile.Station;
                Assert.AreEqual(flying, EM.HasComponent<ShipCommand>(root), profile.ToString());
                Assert.AreEqual(flying, EM.HasComponent<FlightState>(root));
                Assert.AreEqual(flying, EM.HasBuffer<FlightThruster>(root));
                Assert.AreEqual(flying, EM.HasComponent<PropulsionStats>(root));
                Assert.AreEqual(flying, EM.HasComponent<PlanarConstraint>(root));
                Assert.AreEqual(profile == GridProfile.PlayerShip, EM.HasComponent<PlayerShip>(root));
                var instances = EM.GetBuffer<BlockInstance>(root);
                Assert.AreEqual(4, instances.Length);
                foreach (var instance in instances)
                {
                    Assert.IsTrue(EM.HasComponent<ThrusterState>(instance.Functional));
                    Assert.IsTrue(EM.HasComponent<ThrusterVisual>(instance.Functional));
                    Assert.AreEqual(0, EM.GetComponentData<ThrusterState>(instance.Functional).Intensity);
                    Assert.IsTrue(EM.HasComponent<Powered>(instance.Functional));
                    var requests = EM.GetBuffer<PowerRequest>(instance.Functional);
                    Assert.AreEqual(1, requests.Length);
                    Assert.AreEqual(BlockModuleId.Thruster, requests[0].Module);
                    Assert.AreEqual(PowerTuning.Defaults.PriorityThruster, requests[0].Priority);
                    Assert.IsTrue(content.Service.BlockModules.Value.TryGet(BlockModuleId.Thruster, instance.Block, out ThrusterParams row));
                    Assert.AreEqual(row.PowerDraw, requests[0].Draw);
                    Assert.Greater(EM.GetComponentData<ThrusterVisual>(instance.Functional).NozzleRadius, 0);
                }
            }
        }

        [Test]
        public void Visual_MatchesOrientation()
        {
            var root = Spawn(); Frame();
            ref var orientations = ref content.Service.Orientations.Value;
            float cellSize = EM.GetComponentData<VoxelGrid>(root).CellSize;
            foreach (var instance in EM.GetBuffer<BlockInstance>(root))
            {
                ref var definition = ref content.Service.BlockDefs.Value.Blocks[instance.Block];
                var visual = EM.GetComponentData<ThrusterVisual>(instance.Functional);
                Assert.AreEqual(-GridSpaceMath.BlockDirToGrid(new float3(0, 0, -1), ref orientations, instance.Orient), visual.ExhaustDirLocal);
                float3 centre = ((float3)definition.BoundsMin + (float3)definition.BoundsMax) * .5f;
                float3 face = new float3(centre.x, centre.y, definition.BoundsMax.z + .5f);
                float3 expected = ((float3)instance.Origin + .5f + math.rotate(orientations.Rotations[instance.Orient], face)) * cellSize;
                Assert.Less(math.distance(expected, visual.NozzleLocal), .00001f);
                int3 size = definition.BoundsMax - definition.BoundsMin + 1;
                Assert.AreEqual(.5f * math.min(size.x, size.y) * cellSize, visual.NozzleRadius);
            }
        }

        [Test]
        public void ChangeFilter_IdleFrameSkips()
        {
            var root = Spawn(); Frame();
            var entity = EM.GetBuffer<BlockInstance>(root)[0].Functional;
            // If the job runs again, it overwrites this sentinel even though FunctionalBlock did not change.
            var sentinel = new ThrusterVisual { NozzleLocal = new float3(-100), ExhaustDirLocal = new float3(3), NozzleRadius = -1 };
            EM.SetComponentData(entity, sentinel);
            Frame();
            Assert.AreEqual(sentinel, EM.GetComponentData<ThrusterVisual>(entity));
        }
    }
}
