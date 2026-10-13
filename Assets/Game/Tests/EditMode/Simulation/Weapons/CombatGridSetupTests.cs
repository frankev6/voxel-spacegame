using Game.Core.Components;
using Game.Simulation;
using Game.Simulation.Weapons;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>SPEC-21 WPN-011: actual profile conversion and requester-owned combat state.</summary>
    public sealed class CombatGridSetupTests
    {
        VoxelTestContent content;
        BlueprintLibrary library;
        VoxelStructureRegistry registry;
        VoxelStructure source;
        World world;
        SystemHandle spawn;
        int handle;
        EntityManager EM => world.EntityManager;

        [SetUp] public void SetUp()
        {
            content = new VoxelTestContent();
            library = new BlueprintLibrary(content.Service.Catalog, content.Content);
            registry = VoxelStructureRegistry.CreateRuntime(library);
            source = new VoxelStructure();
            source.SetBrickUniform(new int3(0), Cell.Palette(source.FindOrAddPalette(new PaletteEntry { Block = content.Index("block_hull") })));
            handle = registry.Register(source);
            world = new World("WPN-011 combat profiles");
            content.Service.CreateSingletons(EM);
            EM.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
            EM.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
            EM.CreateEntity(typeof(GameplayActive));
            spawn = world.CreateSystem<GridSpawnSystem>();
            world.CreateSystem<WeaponInitSystem>(); // The real owner's OnCreate registers root/functional types.
        }

        [TearDown] public void TearDown()
        {
            world?.Dispose();
            if (registry != null) Object.DestroyImmediate(registry);
            source?.Dispose(); library?.Dispose(); content?.Dispose();
        }

        Entity Request(GridProfile profile) => GridSpawnApi.RequestSpawn(EM, registry, handle, new GridSpawnParams
        {
            PersistentId = (ulong)profile + 1, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender,
            Profile = profile, Motion = GridMotion.Dynamic
        });
        void Convert() { spawn.Update(world.Unmanaged); EM.CompleteAllTrackedJobs(); }

        [Test] public void ProfileAddsComponents()
        {
            var types = CombatGridSetup.RootTypes;
            Assert.AreEqual(3, types.Length);
            using (var query = EM.CreateEntityQuery(typeof(RootComponentRegistration)))
            {
                var rows = EM.GetBuffer<RootComponentRegistration>(query.GetSingletonEntity());
                Assert.AreEqual(15, rows.Length);
                for (GridProfile profile = GridProfile.PlayerShip; profile <= GridProfile.Dummy; profile++)
                {
                    int count = 0;
                    for (int i = 0; i < rows.Length; i++)
                        if (rows[i].Profile == profile)
                        {
                            var type = rows[i].Type;
                            Assert.IsTrue(type == ComponentType.ReadWrite<WeaponControl>() || type == ComponentType.ReadWrite<ShipTarget>() || type == ComponentType.ReadWrite<WeaponStats>());
                            count++;
                        }
                    Assert.AreEqual(3, count, profile.ToString());
                }
            }
            for (GridProfile profile = GridProfile.None; profile <= GridProfile.Dummy; profile++)
            {
                var root = Request(profile); Convert();
                Assert.IsTrue(EM.HasComponent<VoxelGrid>(root));
                Assert.IsFalse(EM.IsComponentEnabled<GridSpawning>(root));
                bool combat = profile != GridProfile.None;
                Assert.AreEqual(combat, EM.HasComponent<WeaponControl>(root), profile.ToString());
                Assert.AreEqual(combat, EM.HasComponent<ShipTarget>(root), profile.ToString());
                Assert.AreEqual(combat, EM.HasComponent<WeaponStats>(root), profile.ToString());
                Assert.IsFalse(EM.HasComponent<Allegiance>(root), "No implicit player allegiance on any profile.");
                if (!combat) continue;
                Assert.AreEqual(default(WeaponControl), EM.GetComponentData<WeaponControl>(root));
                Assert.AreEqual(default(ShipTarget), EM.GetComponentData<ShipTarget>(root));
                Assert.AreEqual(default(WeaponStats), EM.GetComponentData<WeaponStats>(root));
            }
        }

        [Test] public void RequesterAllegiance_Kept()
        {
            var root = Request(GridProfile.NpcShip);
            var allegiance = new Allegiance { Faction = 7, Team = 12 };
            var control = new WeaponControl { Autofire = 1 };
            EM.AddComponentData(root, allegiance);
            EM.AddComponentData(root, control);
            Convert();
            Assert.IsTrue(EM.HasComponent<VoxelGrid>(root));
            Assert.AreEqual(allegiance, EM.GetComponentData<Allegiance>(root));
            Assert.AreEqual(control, EM.GetComponentData<WeaponControl>(root), "Profile conversion must preserve existing registered values too.");
            Assert.IsTrue(EM.HasComponent<ShipTarget>(root)); Assert.IsTrue(EM.HasComponent<WeaponStats>(root));
        }
    }
}
