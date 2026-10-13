using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>PWR-004's five required acceptance tests, using the shipped Fighter and production spawn/rebuild.</summary>
    public sealed class PowerInitSystemTests
    {
        EcsTestWorld world;
        ContentService content;
        BlueprintLibrary library;
        VoxelStructureRegistry registry;
        BlobAssetReference<WeaponDefsBlob> weapons;
        FactionRelations factions;
        EntityManager Em => world.EntityManager;
        static ContentHash Fighter => new ContentId("ship_fighter").Hash;
        [SetUp] public void SetUp()
        {
            content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
            var voxelContent = new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations };
            library = new BlueprintLibrary(content.Catalog, voxelContent); registry = VoxelStructureRegistry.CreateRuntime(library);
            world = EcsTestWorld.Create(typeof(GridSpawnSystem), typeof(PowerRegistrationSystem), typeof(TestConsumerInitSystem), typeof(PowerInitSystem));
            content.CreateSingletons(Em); world.AddSingleton(new VoxelStructureRegistryRef { Value = registry });
            world.AddSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
            world.AddSingleton(PowerTuning.Defaults); world.AddGameplayActive();
            WeaponContent.CreateSingletons(Em, content.Catalog, new List<Entity>());
            using var w = Em.CreateEntityQuery(typeof(WeaponDefsRef)); weapons = w.GetSingleton<WeaponDefsRef>().Value;
            using var f = Em.CreateEntityQuery(typeof(FactionRelations)); factions = f.GetSingleton<FactionRelations>();
        }
        [TearDown] public void TearDown()
        {
            world?.Dispose(); if (weapons.IsCreated) weapons.Dispose(); factions.Dispose();
            if (registry != null) Object.DestroyImmediate(registry); library?.Dispose(); content?.Dispose();
        }
        Entity Spawn(GridProfile profile = GridProfile.PlayerShip)
        {
            var root = GridSpawnApi.RequestSpawnBlueprint(Em, Fighter, new GridSpawnParams { PersistentId = 1, Pose = RigidTransform.identity,
                Profile = profile, Motion = GridMotion.Dynamic, Flags = GridSpawnFlags.NoRender });
            Frame(); return root;
        }
        void Frame() { world.StepFrame(EcsTestWorld.TickSeconds); Em.CompleteAllTrackedJobs(); }
        void VerifyBindings(Entity root)
        {
            var consumers = Em.GetBuffer<PowerConsumer>(root);
            for (int i = 0; i < consumers.Length; i++)
            {
                var c = consumers[i]; var request = Em.GetBuffer<PowerRequest>(c.Functional)[c.RequestIndex];
                Assert.AreEqual(i, request.ConsumerIndex); Assert.AreEqual(c.Module, request.Module);
                Assert.AreEqual(1, Em.GetComponentData<PowerLink>(c.Functional).Bound);
                if (i == 0) continue;
                var previous = consumers[i - 1];
                Assert.IsTrue(previous.Priority > c.Priority || previous.Priority == c.Priority &&
                    (previous.InstanceIndex < c.InstanceIndex || previous.InstanceIndex == c.InstanceIndex && (byte)previous.Module <= (byte)c.Module));
            }
        }
        [Test] public void Spawn_BuildsSortedConsumers()
        {
            var root = Spawn(); VerifyBindings(root); int thrusters = 0, mounts = 0;
            foreach (var c in Em.GetBuffer<PowerConsumer>(root))
            { if (c.Module == BlockModuleId.Thruster) thrusters++; if (c.Module == BlockModuleId.WeaponMount) mounts++; }
            Assert.AreEqual(14, thrusters); Assert.AreEqual(14, mounts);
            var functional = Em.GetBuffer<PowerConsumer>(root)[0].Functional;
            PowerRegistration.SetConsumer(Em.GetBuffer<PowerRequest>(functional), BlockModuleId.ShieldEmitter, 2, 200);
            Em.SetComponentData(functional, default(PowerLink)); Frame(); VerifyBindings(root);
            Assert.AreEqual(2, Em.GetComponentData<PowerLink>(functional).ConsumerCount, "One functional can bind two module requests.");
        }
        [Test] public void Spawn_SetsFullStorage()
        {
            var root = Spawn(); var p = Em.GetComponentData<ShipPower>(root);
            Assert.AreEqual(200, p.Capacity); Assert.AreEqual(200, p.Stored); Assert.AreEqual(40, p.Generation);
            Assert.AreEqual(ShipPowerFlags.Initialized, p.Flags); Assert.AreEqual(8, Em.GetBuffer<ShieldRipple>(root).Length);
            Assert.AreEqual(1, Em.GetComponentData<ShieldControl>(root).RequestedOn);
            Assert.AreEqual(ShieldFlags.FillOnNextUpdate, Em.GetComponentData<ShieldState>(root).Flags);
        }
        [Test] public void Rebuild_Reinitialises()
        {
            var root = Spawn(); int previous = Em.GetBuffer<PowerConsumer>(root).Length;
            Assert.IsTrue(library.TryGet(Fighter, out var source)); using var changed = source.Clone();
            ushort block = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_shield_small")));
            Assert.AreEqual(PlaceResult.Ok, changed.TryPlaceBlock(new BlockPlacement { Block = block, Origin = new int3(80,0,0) }, new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations }));
            Em.SetComponentData(root, new ShieldControl { RequestedOn = 0 });
            GridSpawnApi.RequestRebuild(Em, registry, root, registry.Register(changed), GridSpawnFlags.NoRender); Frame();
            Assert.AreEqual(previous + 1, Em.GetBuffer<PowerConsumer>(root).Length); VerifyBindings(root);
            Assert.AreEqual(BlockModuleId.ShieldEmitter, Em.GetBuffer<PowerConsumer>(root)[0].Module);
            Assert.AreEqual(0, Em.GetComponentData<ShieldControl>(root).RequestedOn, "Reinitialization preserves the player's shield toggle.");
        }
        [Test] public void NoShipPower_Ignored()
        {
            var root = Spawn(GridProfile.None); Assert.IsFalse(Em.HasComponent<ShipPower>(root));
            foreach (var instance in Em.GetBuffer<BlockInstance>(root))
                if (Em.HasComponent<PowerLink>(instance.Functional)) Assert.AreEqual(0, Em.GetComponentData<PowerLink>(instance.Functional).Bound);
        }
        [Test] public void NonConsumer_IndexMinusOne()
        {
            var root = Spawn(); int nonConsumers = 0;
            foreach (var instance in Em.GetBuffer<BlockInstance>(root))
            {
                if (instance.Functional == Entity.Null || Em.HasBuffer<PowerRequest>(instance.Functional)) continue;
                nonConsumers++; Assert.IsFalse(Em.HasComponent<PowerLink>(instance.Functional));
            }
            Assert.Greater(nonConsumers, 0); VerifyBindings(root);
            var entity = Em.CreateEntity(); var requests = Em.AddBuffer<PowerRequest>(entity);
            PowerRegistration.SetConsumer(requests, BlockModuleId.Thruster, 3, 150); Assert.AreEqual(-1, requests[0].ConsumerIndex);
            PowerRegistration.SetRequesting(requests, BlockModuleId.Thruster, true); Assert.AreEqual(1, requests[0].Requesting);
            PowerRegistration.SetRequesting(requests, BlockModuleId.WeaponMount, true); Assert.AreEqual(1, requests.Length);
        }
    }
}
