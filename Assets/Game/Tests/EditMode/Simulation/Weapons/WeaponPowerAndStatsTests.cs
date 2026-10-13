using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>WPN-018's five required checks use the real Fighter and production command, demand, binding and stats systems.</summary>
    public sealed class WeaponPowerAndStatsTests : GameTestBase
    {
        EcsTestWorld world;
        ContentService content;
        BlueprintLibrary library;
        VoxelStructureRegistry registry;
        BlobAssetReference<WeaponDefsBlob> weapons;
        Entity root, player;
        const byte Priority = 97;
        EntityManager Em => world.EntityManager;
        WeaponStats Stats => Em.GetComponentData<WeaponStats>(root);

        [SetUp] public void SetUp()
        {
            content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
            weapons = WeaponContent.BuildBlob(content.Catalog);
            library = new BlueprintLibrary(content.Catalog, new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations });
            registry = VoxelStructureRegistry.CreateRuntime(library);
            world = CreateWorld(typeof(GridSpawnSystem), typeof(WeaponInitSystem), typeof(ThrusterInitSystem),
                typeof(PowerRegistrationSystem), typeof(PowerInitSystem), typeof(PlayerCommandApplySystem),
                typeof(WeaponPowerDemandSystem), typeof(PowerAllocationSystem), typeof(ShipStatsAggregateSystem), typeof(WeaponStatsSystem));
            content.CreateSingletons(Em); world.AddSingleton(new WeaponDefsRef { Value = weapons });
            world.AddSingleton(new VoxelStructureRegistryRef { Value = registry });
            world.AddSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
            var tuning = PowerTuning.Defaults; tuning.PriorityWeapon = Priority; world.AddSingleton(tuning);
            player = world.AddSingleton(default(PlayerCommand)); world.AddGameplayActive(); world.AddSimClock();
            root = GridSpawnApi.RequestSpawnBlueprint(Em, new ContentId("ship_fighter").Hash, new GridSpawnParams
            {
                PersistentId = 1, Pose = RigidTransform.identity, Profile = GridProfile.PlayerShip,
                Motion = GridMotion.Dynamic, Flags = GridSpawnFlags.NoRender
            });
            Tick(); Assert.IsFalse(Em.IsComponentEnabled<GridSpawning>(root));
        }
        [TearDown] public void Clean()
        {
            DisposeTrackedWorlds();
            if (registry != null) Object.DestroyImmediate(registry); library?.Dispose();
            if (weapons.IsCreated) weapons.Dispose(); weapons = default; content?.Dispose();
        }
        void Tick() => world.StepTicks(1);
        void Hold(uint groups) => Em.SetComponentData(player, new PlayerCommand { FireGroups = groups });
        PowerRequest Request(Entity functional)
        {
            foreach (var request in Em.GetBuffer<PowerRequest>(functional)) if (request.Module == BlockModuleId.WeaponMount) return request;
            Assert.Fail("WeaponMount power element missing."); return default;
        }
        void AssertDemand(uint groups, bool autofire = false)
        {
            int count = 0; float expectedDraw = 0;
            foreach (var instance in Em.GetBuffer<BlockInstance>(root))
            {
                if (!Em.HasComponent<WeaponMount>(instance.Functional)) continue;
                count++; var mount = Em.GetComponentData<WeaponMount>(instance.Functional);
                bool expected = mount.WeaponIndex != 0 && ((groups & Em.GetComponentData<WeaponState>(instance.Functional).Group) != 0 || autofire);
                Assert.AreEqual(expected ? 1 : 0, Request(instance.Functional).Requesting);
                if (expected) expectedDraw += weapons.Value.Weapons[mount.WeaponIndex].PowerDraw;
            }
            Assert.AreEqual(14, count);
            Assert.AreEqual(expectedDraw, Em.GetComponentData<ShipPower>(root).DemandPerSecond, .001f, "The allocator must consume this tick's feature demand.");
            Assert.AreEqual(expectedDraw, Em.GetComponentData<ShipPower>(root).GrantedPerSecond, .001f);
        }

        [Test] public void Init_SetsConsumer()
        {
            int count = 0;
            foreach (var instance in Em.GetBuffer<BlockInstance>(root))
            {
                if (!Em.HasComponent<WeaponMount>(instance.Functional)) continue;
                count++; var mount = Em.GetComponentData<WeaponMount>(instance.Functional); var request = Request(instance.Functional);
                Assert.AreEqual(weapons.Value.Weapons[mount.WeaponIndex].PowerDraw, request.Draw);
                Assert.AreEqual(Priority, request.Priority); Assert.AreEqual(0, request.Requesting);
                Assert.AreEqual(1, Em.GetComponentData<PowerLink>(instance.Functional).Bound); Assert.IsTrue(Em.HasComponent<Powered>(instance.Functional));
                var consumer = Em.GetBuffer<PowerConsumer>(root)[request.ConsumerIndex];
                Assert.AreEqual(instance.Functional, consumer.Functional); Assert.AreEqual(BlockModuleId.WeaponMount, consumer.Module);
                Assert.AreEqual(request.Draw, consumer.Draw); Assert.AreEqual(Priority, consumer.Priority);
            }
            Assert.AreEqual(14, count); Tick();
            foreach (var instance in Em.GetBuffer<BlockInstance>(root))
                if (Em.HasComponent<WeaponMount>(instance.Functional)) Assert.AreEqual(Priority, Request(instance.Functional).Priority);
        }
        [Test] public void Demand_HeldGroup_SameTick()
        {
            Assert.AreEqual(0, Em.GetComponentData<ShipCommand>(root).FireGroups);
            Hold(1); Tick(); Assert.AreEqual(1, Em.GetComponentData<ShipCommand>(root).FireGroups); AssertDemand(1);
            Hold(2); Tick(); AssertDemand(2);
            Hold(3); Tick(); AssertDemand(3);
            Hold(0); Tick(); AssertDemand(0);
        }
        [Test] public void Demand_Autofire()
        {
            Em.SetComponentData(root, new WeaponControl { Autofire = 1 }); Tick(); AssertDemand(0);
            var target = Em.CreateEntity(); Em.SetComponentData(root, new ShipTarget { Value = target }); Tick(); AssertDemand(0, true);
            Em.SetComponentData(root, default(WeaponControl)); Tick(); AssertDemand(0);
            Em.SetComponentData(root, new WeaponControl { Autofire = 1 }); Em.SetComponentData(root, default(ShipTarget)); Tick(); AssertDemand(0);
            // Missing root input and an unarmed mount clear only the weapon element; other modules keep their owner-written demand.
            var orphan = Em.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount), typeof(WeaponState));
            Em.SetComponentData(orphan, new WeaponState { Group = 1 });
            var requests = Em.AddBuffer<PowerRequest>(orphan);
            requests.Add(new PowerRequest { Module = BlockModuleId.WeaponMount, Requesting = 1 });
            requests.Add(new PowerRequest { Module = BlockModuleId.Thruster, Requesting = 1 });
            Tick(); Assert.AreEqual(0, Request(orphan).Requesting); Assert.AreEqual(1, Em.GetBuffer<PowerRequest>(orphan)[1].Requesting);
        }
        [Test] public void Stats_Spawn()
        {
            Assert.AreEqual(14, Stats.AliveWeaponCount); Assert.AreEqual(14, Stats.InitialWeaponCount);
            // Real export: 8 fixed small plasma, 4 small beams, 2 medium plasma; independent SPEC-21 §4.9 oracle.
            Assert.AreEqual(8 * 150 + 4 * 90 + 2 * 315, Stats.SustainedDps, .001f);
            Assert.AreEqual(8 * 1.5f + 4 * 4 + 2 * 4, Stats.WeaponDraw, .001f);
            Assert.AreEqual(450 * 1.6f, Stats.MaxWeaponRange, .001f);
            Assert.AreEqual(1u, Stats.SourceRevision); Assert.AreEqual(Em.GetComponentData<ShipStats>(root).FunctionalRevision, Stats.SourceRevision);
        }
        [Test] public void Stats_WeaponDestroyed()
        {
            var before = Stats; float removedDps = 0, removedDraw = 0; bool removed = false;
            var instances = Em.GetBuffer<BlockInstance>(root);
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i]; if (!Em.HasComponent<WeaponMount>(instance.Functional)) continue;
                ref var definition = ref weapons.Value.Weapons[Em.GetComponentData<WeaponMount>(instance.Functional).WeaponIndex];
                removedDps = definition.SustainedDps; removedDraw = definition.PowerDraw;
                instance.Flags |= BlockInstanceFlags.Destroyed; instances[i] = instance; removed = true; break;
            }
            Assert.IsTrue(removed); Tick();
            Assert.AreEqual(13, Stats.AliveWeaponCount); Assert.AreEqual(14, Stats.InitialWeaponCount);
            Assert.AreEqual(before.SustainedDps - removedDps, Stats.SustainedDps, .001f);
            Assert.AreEqual(before.WeaponDraw - removedDraw, Stats.WeaponDraw, .001f);
            Assert.AreEqual(before.SourceRevision + 1, Stats.SourceRevision);
            Assert.AreEqual(Em.GetComponentData<ShipStats>(root).FunctionalRevision, Stats.SourceRevision);
            var sentinel = Stats; sentinel.WeaponDraw = -123; Em.SetComponentData(root, sentinel);
            Tick(); Assert.AreEqual(sentinel, Stats, "An unchanged revision must not rescan the instances.");
            // Sleeping, spawning and hulk roots retain their totals until their existing lifecycle permits a rebuild.
            if (!Em.HasComponent<Dormant>(root)) Em.AddComponent<Dormant>(root);
            Em.SetComponentEnabled<Dormant>(root, true); ForceRevision(); Tick(); Assert.AreEqual(sentinel, Stats);
            Em.SetComponentEnabled<Dormant>(root, false); Tick(); Assert.AreEqual(13, Stats.AliveWeaponCount); Assert.AreEqual(14, Stats.InitialWeaponCount);
            sentinel = Stats; Em.SetComponentEnabled<GridSpawning>(root, true); ForceRevision(); Tick(); Assert.AreEqual(sentinel, Stats);
            Em.SetComponentEnabled<GridSpawning>(root, false); Tick(); sentinel = Stats;
            Em.AddComponent<Hulk>(root); ForceRevision(); Tick(); Assert.AreEqual(sentinel, Stats);
        }
        void ForceRevision()
        { var stats = Em.GetComponentData<ShipStats>(root); stats.FunctionalRevision++; Em.SetComponentData(root, stats); }
    }
}
