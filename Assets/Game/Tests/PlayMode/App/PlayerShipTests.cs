using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.App.Boot;
using Game.App.Flow;
using Game.App.Services;
using Game.App.Session;
using Game.App.Systems;
using Game.Core;
using Game.Core.Components;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.App
{
    /// <summary>FND-010: actual flow/save pipeline, paused body readiness and damage writeback (SPEC-01 §5.7).</summary>
    public sealed class PlayerShipTests
    {
        sealed class TestFleetInitializer : INewGameInitializer
        {
            readonly GameSession session; readonly ushort hull;
            public TestFleetInitializer(GameSession session, ushort hull) { this.session = session; this.hull = hull; }
            public int NewGameOrder => 0;
            public void InitializeNewGame(in NewGameArgs args)
                => session.Fleet.ReplaceStructure(session.Fleet.Flagship.PersistentId, BenchmarkStructures.Ship20k(hull));
        }
        sealed class Fixture : IDisposable
        {
            public readonly EcsTestWorld World;
            public readonly ContentService Content;
            readonly BlueprintLibrary blueprints;
            readonly VoxelStructureRegistry registry;
            readonly PauseService pause = new PauseService();
            public readonly SaveService Save;
            public readonly SceneFlow Flow;
            public readonly GameSession Session;
            readonly string directory = Path.Combine(Application.temporaryCachePath, "Fleet_" + Guid.NewGuid().ToString("N"));
            public EntityManager EM => World.EntityManager;
            public Fixture(bool largeShip)
            {
                Content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
                var config = Content.Catalog.Get<GameConfigDefinition>(new ContentId("config_game"));
                blueprints = new BlueprintLibrary(Content.Catalog, new VoxelContent { Blocks = Content.BlockDefs, Orientations = Content.Orientations });
                registry = VoxelStructureRegistry.CreateRuntime(blueprints);
                World = EcsTestWorld.Create(typeof(GridSpawnSystem), typeof(GridSpawnBodyInitSystem), typeof(ThrusterInitSystem),
                    typeof(SimClockAdvanceSystem), typeof(GridColliderCleanupSystem), typeof(DeferredDisposalSystem));
                Content.CreateSingletons(EM); BootSingletons.CreateAll(EM, Content.Catalog);
                // Rendering has its own M1 integration task; this fixture tests physical/player readiness headlessly.
                using (var q = EM.CreateEntityQuery(typeof(VoxelConfig)))
                { var voxel = q.GetSingleton<VoxelConfig>(); voxel.RequireRenderer = 0; q.SetSingleton(voxel); }
                EM.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                Save = new SaveService(new SavePaths(directory), Content, pause, new SaveServiceConfig());
                Flow = new SceneFlow(World.World, pause, Save, config); Flow.RegisterExiting(registry);
                Session = new GameSession(Flow, Save, World.World, Content, config);
                Session.BindFleet(blueprints, registry, Content.Catalog);
                Save.AttachContext(Session); Save.Register(new SessionSaveParticipant(Session)); Save.Register(Session.FleetParticipant);
                if (largeShip) Session.RegisterNewGameInitializer(new TestFleetInitializer(Session,
                    Content.Catalog.GetBlockIndex(Content.Catalog.Get<BlockDefinition>(new ContentId("block_hull")))));
                Flow.SnapshotBootEntities();
            }
            public IEnumerator Run(Awaitable<FlowResult> operation)
            {
                var awaiter = operation.GetAwaiter(); double deadline = Time.realtimeSinceStartupAsDouble + 30;
                while (!awaiter.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    // A loading pause must not require a gameplay tick to reach the ready hook's collider condition.
                    World.StepFrame(0); yield return null;
                }
                Assert.IsTrue(awaiter.IsCompleted, "Flow did not finish in 30 seconds");
                Assert.AreEqual(FlowResult.Ok, awaiter.GetResult());
            }
            public Entity Root()
            { Assert.IsTrue(Session.PlayerSpawner.TryGetRoot(out var root)); return root; }
            public void Dispose()
            {
                Session.Dispose(); Flow.Dispose(); Save.Dispose(); World.Dispose();
                Object.DestroyImmediate(registry); blueprints.Dispose(); Content.Dispose(); pause.Dispose();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [UnityTest]
        public IEnumerator Spawn_AtStart()
        {
            using var f = new Fixture(false);
            yield return f.Run(f.Flow.TransitionAsync(GameScene.MainMenu, SceneEntryReason.Startup));
            yield return f.Run(f.Session.NewGameAsync(new NewGameArgs("Pilot", 123, false, ContentId.None)));
            var root = f.Root();
            Assert.AreEqual(f.Session.Fleet.Flagship.PersistentId, f.EM.GetComponentData<PersistentId>(root).Value);
            Assert.IsTrue(f.EM.HasComponent<PlayerShip>(root));
            Assert.IsTrue(f.EM.HasComponent<PhysicsCollider>(root));
            Assert.IsFalse(f.EM.IsComponentEnabled<GridSpawning>(root));
            Assert.AreEqual(float3.zero, f.EM.GetComponentData<LocalTransform>(root).Position);
            Assert.AreEqual(0, f.World.TicksRun, "Loading completed while gameplay remained paused");
            foreach (var member in f.EM.GetBuffer<LinkedEntityGroup>(root))
            {
                using var types = f.EM.GetComponentTypes(member.Value);
                foreach (var type in types) Assert.AreNotEqual("StarSystemMember", TypeManager.GetType(type.TypeIndex).Name);
            }
            var record = f.Session.Fleet.Flagship;
            yield return f.Run(f.Session.QuitToMainMenuAsync(false));
            Assert.AreEqual(-1, record.StructureHandle); Assert.IsNull(record.Structure);
        }

        [UnityTest]
        public IEnumerator Damage_SurvivesSaveLoad()
        {
            using var f = new Fixture(true);
            yield return f.Run(f.Flow.TransitionAsync(GameScene.MainMenu, SceneEntryReason.Startup));
            yield return f.Run(f.Session.NewGameAsync(new NewGameArgs("Pilot", 123, false, ContentId.None)));
            ulong id = f.Session.Fleet.Flagship.PersistentId;
            var root = f.Root(); var grid = f.EM.GetComponentData<VoxelGrid>(root); uint revision = ++grid.Revision;
            var removed = new List<int3>();
            foreach (var slot in f.EM.GetBuffer<BrickSlot>(root))
            {
                if (slot.Brick == Entity.Null || removed.Count == 50) continue;
                var brick = f.EM.GetComponentData<Brick>(slot.Brick); var dirty = f.EM.GetComponentData<BrickDirty>(slot.Brick);
                var dense = f.EM.GetBuffer<BrickCells>(slot.Brick); var sparse = f.EM.GetBuffer<BrickSparseCells>(slot.Brick);
                var mask = f.EM.GetBuffer<BrickSolidMask>(slot.Brick);
                for (int i = 0; i < BrickMath.CellsPerBrick && removed.Count < 50; i++)
                    if (!BrickWrite.Get(dense, sparse, i).IsAir)
                    {
                        Assert.IsTrue(BrickWrite.Set(ref brick, dense, sparse, mask, ref dirty, i, default, revision));
                        removed.Add(BrickMath.CellOf(brick.Coord, i));
                    }
                f.EM.SetComponentData(slot.Brick, brick); f.EM.SetComponentData(slot.Brick, dirty); f.EM.SetComponentEnabled<BrickDirty>(slot.Brick, true);
            }
            f.EM.SetComponentData(root, grid); Assert.AreEqual(50, removed.Count);
            var slotId = new SlotId("manual_001");
            yield return f.Run(f.Session.SaveGameAsync(slotId.Value, SaveReason.Manual));
            Assert.AreEqual(19950, f.Session.Fleet.Flagship.Structure.SolidCellCount, "Capture prepared the live structure");
            yield return f.Run(f.Session.QuitToMainMenuAsync(false));
            yield return f.Run(f.Session.LoadGameAsync(slotId.Value));
            root = f.Root(); Assert.AreEqual(id, f.EM.GetComponentData<PersistentId>(root).Value);
            Assert.AreEqual(19950, f.Session.Fleet.Flagship.Structure.SolidCellCount);
            grid = f.EM.GetComponentData<VoxelGrid>(root);
            foreach (var cell in removed)
            {
                Assert.IsTrue(f.Session.Fleet.Flagship.Structure.GetCell(cell).IsAir);
                int index = BrickMath.SlotIndex(BrickMath.BrickOf(cell), grid.BrickMin, grid.BrickDims);
                var brick = f.EM.GetBuffer<BrickSlot>(root)[index].Brick;
                Assert.IsTrue(BrickWrite.Get(f.EM.GetBuffer<BrickCells>(brick), f.EM.GetBuffer<BrickSparseCells>(brick), BrickMath.LocalIndex(BrickMath.LocalOf(cell))).IsAir);
            }
            yield return f.Run(f.Session.QuitToMainMenuAsync(false));
        }
    }
}
