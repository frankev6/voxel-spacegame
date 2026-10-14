using System;
using System.Collections.Generic;
using System.Linq;
using Game.App.Boot;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Economy;
using Game.Data.Services;
using Game.Economy;
using Game.Galaxy.Bodies;
using Game.Galaxy.Interaction;
using Game.Galaxy.Salvage;
using Game.Simulation.Commands;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.PlayMode.Galaxy
{
    public sealed class SalvageTests
    {
        internal sealed class Audio : IAudioService
        {
            internal int Calls;
            public void PlayCue(ContentId cue) => Calls++;
            public void PlayUi(UiSound sound) { }
            public void PlayCueAt(ContentId cue, float3 position, float volume) { }
            public void PlayStinger(ContentId cue) { }
            public void SetMusicOverride(ContentId cue, float fadeSeconds) { }
            public void ClearMusicOverride(float fadeSeconds) { }
        }
        internal sealed class Fixture : IDisposable
        {
            internal readonly ContentService Content = new(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
            internal readonly EcsTestWorld Test;
            internal EntityManager Em => Test.EntityManager;
            internal readonly EconomyConfigDefinition Config;
            internal readonly InventoryModel Cargo;
            internal readonly Audio Sound = new();
            internal readonly SalvageService Salvage;
            internal readonly BlockDefinition Thruster;
            readonly ICargo previous;
            internal Fixture(bool allIntact = false)
            {
                Test = EcsTestWorld.Create(typeof(SalvageMarkSystem), typeof(GridSpawnSystem));
                Test.AddGameplayActive(); Test.AddSimClock(); Em.CreateEntity(typeof(WorldSceneActive));
                Config = UnityEngine.Object.Instantiate(Content.Catalog.Get<EconomyConfigDefinition>(new ContentId("config_economy")));
                if (allIntact) JsonUtility.FromJsonOverwrite("{\"salvageFunctionalChance\":1}", Config);
                Em.CreateSingleton(EconomyConfigRefFactory.Create(Config));
                Cargo = new InventoryModel(Content.Catalog);
                Services.TryGet<ICargo>(out previous); if (previous != null) Services.Unregister(previous); Services.Register<ICargo>(Cargo);
                Salvage = new SalvageService(Test.World, Content.Catalog, Config, Sound);
                Thruster = Content.Catalog.All<BlockDefinition>().First(block => block.Kind == BlockKind.Functional && block.IdString.Contains("thruster"));
            }
            internal Entity Hulk(uint cells = 5000, ulong id = 77, int instances = 3)
            {
                var root = Em.CreateEntity(typeof(Hulk), typeof(HulkInfo), typeof(VoxelGrid), typeof(PersistentId), typeof(GridMass), typeof(GridDespawnRequest), typeof(BlockInstance));
                Em.SetComponentEnabled<GridDespawnRequest>(root, false); Em.SetComponentData(root, new PersistentId(id));
                Em.SetComponentData(root, new HulkInfo { Tick = 31 });
                Em.SetComponentData(root, new GridMass { Data = new StructureMassData { CellCount = cells } });
                var buffer = Em.GetBuffer<BlockInstance>(root);
                for (int i = 0; i < instances; i++) buffer.Add(new BlockInstance { Block = Content.Catalog.GetBlockIndex(Thruster), CellsAlive = (ushort)Thruster.Cells.Count });
                return root;
            }
            internal List<SalvageYield> Yields(Entity root)
            {
                var list = new List<SalvageYield>(); SalvageService.ComputeYields(Em.GetBuffer<BlockInstance>(root), Em.GetComponentData<GridMass>(root).Data.CellCount,
                    StableHash.Combine(Em.GetComponentData<PersistentId>(root).Value, 31), Content.Catalog, Config, list); return list;
            }
            public void Dispose()
            {
                Salvage.Dispose(); Services.Unregister<ICargo>(Cargo); if (previous != null) Services.Register(previous);
                Test.Dispose(); Content.Dispose(); UnityEngine.Object.DestroyImmediate(Config);
            }
        }
        [Test] public void Mark_NewHulksOnly()
        {
            using var f = new Fixture(); var root = f.Hulk(); var player = f.Hulk(id: 78); f.Em.AddComponent<PlayerShip>(player);
            var body = f.Hulk(id: 79); f.Em.AddComponent<BodyGridOwner>(body);
            var spawning = f.Hulk(id: 80); f.Em.AddComponent<GridSpawning>(spawning);
            var system = f.Test.World.GetExistingSystemManaged<SalvageMarkSystem>(); system.Update();
            Assert.AreEqual(InteractionKind.Salvage, f.Em.GetComponentData<Interactable>(root).Kind);
            Assert.AreEqual(f.Config.SalvageDistanceM, f.Em.GetComponentData<Interactable>(root).DistanceM);
            Assert.IsFalse(f.Em.HasComponent<Interactable>(player)); Assert.IsFalse(f.Em.HasComponent<Interactable>(body)); Assert.IsFalse(f.Em.HasComponent<Interactable>(spawning));
            f.Em.SetComponentEnabled<GridSpawning>(spawning, false); system.Update(); Assert.IsTrue(f.Em.HasComponent<Interactable>(spawning));
        }
        [Test] public void Yields_Deterministic()
        {
            using var f = new Fixture(); var root = f.Hulk(); var first = f.Yields(root); var second = f.Yields(root);
            CollectionAssert.AreEqual(first, second);
            int scrap = f.Content.Catalog.IndexOf(f.Config.ScrapItem); Assert.AreEqual(100, first.Single(value => value.ItemIndex == scrap).Count);
            var rng = RngStreams.For(StableHash.Combine(77, 31), RngPurpose.Salvage, 0); int expected = 0;
            for (int i = 0; i < 3; i++) if (rng.NextFloat() < f.Config.SalvageFunctionalChance) expected++;
            Assert.AreEqual(expected, first.Where(value => value.ItemIndex == f.Content.Catalog.IndexOf<ItemDefinition>(f.Thruster)).Sum(value => value.Count));
        }
        [Test] public void Partial_ThenComplete()
        {
            using var f = new Fixture(true); var root = f.Hulk();
            var thrusters = f.Content.Catalog.All<BlockDefinition>().Where(block => block.Kind == BlockKind.Functional && block.IdString.Contains("thruster")).Take(3).ToArray();
            Assert.AreEqual(3, thrusters.Length); var instances = f.Em.GetBuffer<BlockInstance>(root);
            for (int i = 0; i < 3; i++) instances[i] = new BlockInstance { Block = f.Content.Catalog.GetBlockIndex(thrusters[i]), CellsAlive = (ushort)thrusters[i].Cells.Count };
            var expected = f.Yields(root); f.Cargo.SetCapacity(2);
            Assert.IsTrue(f.Salvage.TrySalvage(root, out string message)); StringAssert.Contains("Cargo full", message);
            Assert.AreEqual(1, f.Salvage.PendingWrecks); Assert.IsFalse(f.Em.IsComponentEnabled<GridDespawnRequest>(root));
            f.Cargo.SetCapacity(20); Assert.IsTrue(f.Salvage.TrySalvage(root, out message)); Assert.AreEqual(0, f.Salvage.PendingWrecks);
            foreach (var item in expected) Assert.AreEqual(item.Count, f.Cargo.CountOf(item.ItemIndex));
            Assert.IsTrue(f.Em.IsComponentEnabled<GridDespawnRequest>(root)); Assert.AreEqual(1, f.Sound.Calls);
            Assert.IsFalse(f.Salvage.TrySalvage(root, out _), "Pending despawn cannot be harvested again.");
        }
        [Test] public void TooLarge_Refused()
        {
            using var f = new Fixture(); var root = f.Hulk(30000);
            Assert.IsFalse(f.Salvage.TrySalvage(root, out string message)); Assert.AreEqual("Too large to salvage.", message);
            Assert.AreEqual(0, f.Salvage.PendingWrecks); Assert.IsFalse(f.Em.IsComponentEnabled<GridDespawnRequest>(root));
        }
        [Test] public void Despawn_WhenEmpty()
        {
            using var f = new Fixture(); f.Content.CreateSingletons(f.Em);
            var content = new VoxelContent { Blocks = f.Content.BlockDefs, Orientations = f.Content.Orientations };
            using var library = new BlueprintLibrary(f.Content.Catalog, content);
            using var source = new VoxelStructure();
            ushort palette = source.FindOrAddPalette(new PaletteEntry { Block = f.Content.Catalog.GetBlockIndex(f.Content.Catalog.Get<BlockDefinition>(new ContentId("block_hull"))) });
            source.SetCell(int3.zero, Cell.Palette(palette));
            var registry = VoxelStructureRegistry.CreateRuntime(library);
            try
            {
                f.Em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry }); f.Em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024 });
                int handle = registry.Register(source);
                var root = GridSpawnApi.RequestSpawn(f.Em, registry, handle, new GridSpawnParams { PersistentId = 123, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender });
                f.Test.StepTicks(1); Assert.IsFalse(f.Em.IsComponentEnabled<GridSpawning>(root));
                f.Em.AddComponent<Hulk>(root); f.Em.AddComponentData(root, new HulkInfo { Tick = 31 });
                Assert.IsTrue(f.Salvage.TrySalvage(root, out _)); f.Test.StepTicks(1); Assert.IsFalse(f.Em.Exists(root));
                Assert.AreEqual(1, f.Sound.Calls); Assert.AreEqual(0, f.Salvage.PendingWrecks);
            }
            finally { registry.Dispose(); }
        }
    }
    [Category("Performance")]
    public sealed class SalvagePerfTests
    {
        [Test] public void Mark_NoHulks_Cost()
        {
            using var f = new SalvageTests.Fixture(); var system = f.Test.World.GetExistingSystemManaged<SalvageMarkSystem>();
            for (int i = 0; i < 10; i++) system.Update(); var samples = new double[10];
            for (int i = 0; i < samples.Length; i++) { long start = System.Diagnostics.Stopwatch.GetTimestamp(); system.Update(); samples[i] = (System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency; }
            PerfRun.Gate("SalvageMarkNoHulks", samples, .005);
        }
        [Test] public void Yields_2000Instances()
        {
            using var f = new SalvageTests.Fixture(); var root = f.Hulk(20000, instances: 2000); var yields = new List<SalvageYield>(); var samples = new double[10];
            for (int i = 0; i < samples.Length; i++) { long start = System.Diagnostics.Stopwatch.GetTimestamp(); SalvageService.ComputeYields(f.Em.GetBuffer<BlockInstance>(root), 20000, 1, f.Content.Catalog, f.Config, yields); samples[i] = (System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency; }
            Assert.IsNotEmpty(yields); PerfRun.Gate("SalvageYields2000", samples, .5);
        }
    }
}
