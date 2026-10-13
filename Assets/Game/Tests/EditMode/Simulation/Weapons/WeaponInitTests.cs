using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation;
using Game.Simulation.Common;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>WPN-004: real Fighter spawn, all-orientation geometry and reparented state (SPEC-21 §5.2).</summary>
    public sealed class WeaponInitTests
    {
        ContentService content;
        BlueprintLibrary library;
        VoxelStructureRegistry registry;
        EcsTestWorld world;
        BlobAssetReference<WeaponDefsBlob> weapons;
        EntityManager EM => world.EntityManager;

        [SetUp]
        public void SetUp()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset");
            Assert.That(database, Is.Not.Null);
            content = new ContentService(database);
            weapons = WeaponContent.BuildBlob(content.Catalog);
            library = new BlueprintLibrary(content.Catalog, new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations });
            registry = VoxelStructureRegistry.CreateRuntime(library);
            world = EcsTestWorld.Create(typeof(GridSpawnSystem), typeof(WeaponInitSystem));
            content.CreateSingletons(EM);
            world.AddSingleton(new WeaponDefsRef { Value = weapons });
            world.AddSingleton(new VoxelStructureRegistryRef { Value = registry });
            world.AddSingleton(new VoxelConfig { DefaultCellSize = .5f, SpawnBricksPerFrame = 1024 });
            world.AddGameplayActive();
        }

        [TearDown]
        public void TearDown()
        {
            world?.Dispose();
            if (registry != null) Object.DestroyImmediate(registry);
            library?.Dispose();
            if (weapons.IsCreated) weapons.Dispose();
            weapons = default; content?.Dispose();
        }

        void Frame() => world.StepFrame(EcsTestWorld.TickSeconds);
        Entity SpawnFighter()
        {
            var root = GridSpawnApi.RequestSpawnBlueprint(EM, new ContentId("ship_fighter").Hash,
                new GridSpawnParams { PersistentId = 1, Pose = RigidTransform.identity, Motion = GridMotion.Dynamic,
                    Profile = GridProfile.PlayerShip, Flags = GridSpawnFlags.NoRender });
            Frame();
            Assert.That(EM.HasComponent<VoxelGrid>(root), Is.True);
            Assert.That(EM.IsComponentEnabled<GridSpawning>(root), Is.False);
            return root;
        }

        [Test]
        public void Mounts_Initialized()
        {
            var root = SpawnFighter();
            Assert.That(EM.HasComponent<WeaponControl>(root), Is.True);
            Assert.That(EM.HasComponent<ShipTarget>(root), Is.True);
            Assert.That(EM.HasComponent<WeaponStats>(root), Is.True);
            var modules = world.GetSingleton<BlockModulesRef>().Value;
            int count = 0;
            foreach (var instance in EM.GetBuffer<BlockInstance>(root))
            {
                if (!modules.Value.TryGet(BlockModuleId.WeaponMount, instance.Block, out WeaponMountParams row)) continue;
                count++;
                Assert.That(EM.Exists(instance.Functional), Is.True);
                Assert.That(EM.HasComponent<WeaponMount>(instance.Functional), Is.True);
                Assert.That(EM.HasComponent<WeaponState>(instance.Functional), Is.True);
                Assert.That(EM.HasComponent<WeaponAim>(instance.Functional), Is.True);
                Assert.That(EM.HasComponent<BeamState>(instance.Functional), Is.True);
                var mount = EM.GetComponentData<WeaponMount>(instance.Functional);
                var state = EM.GetComponentData<WeaponState>(instance.Functional);
                var beam = EM.GetComponentData<BeamState>(instance.Functional);
                ref var definition = ref weapons.Value.Weapons[row.WeaponIndex];
                Assert.That(mount.Initialized, Is.EqualTo(1)); Assert.That(mount.WeaponIndex, Is.EqualTo(row.WeaponIndex));
                Assert.That(state.Group, Is.EqualTo(definition.DefaultFireGroups)); Assert.That(state.CooldownTicks, Is.EqualTo(definition.TriggerIntervalTicks));
                Assert.That(state.CooldownRemainingTicks, Is.Zero); Assert.That(state.BurstRemainingTicks, Is.Zero);
                Assert.That(state.RoundsLeftInBurst, Is.Zero); Assert.That(state.ShotCounter, Is.Zero); Assert.That(state.Firing, Is.Zero);
                Assert.That(beam.Width, Is.EqualTo(definition.BeamWidth)); Assert.That(beam.Fx, Is.EqualTo(definition.HitFx));
                Assert.That(beam.Active, Is.Zero); Assert.That(beam.TicksLeft, Is.Zero);
                AssertGeometry(mount, instance, row, EM.GetComponentData<VoxelGrid>(root).CellSize);
            }
            Assert.That(count, Is.EqualTo(14), "The shipped Fighter has fourteen canonical weapon mounts (SPEC-21 §5.12).");
        }

        [Test]
        public void Muzzle_MatchesOrientation()
        {
            // Geometry test doubles cover all 24 orientations independently of authored placement masks.
            var grid = EM.CreateEntity(typeof(VoxelGrid));
            EM.SetComponentData(grid, new VoxelGrid { CellSize = .25f, Kind = GridKind.Ship });
            EM.AddBuffer<BlockInstance>(grid);
            foreach (string id in new[] { "weapon_plasma_turret_medium", "weapon_small_beam_turret" })
            {
                ushort block = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId(id)));
                for (byte orientation = 0; orientation < 24; orientation++)
                {
                    var entity = EM.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount), typeof(WeaponState), typeof(WeaponAim), typeof(BeamState));
                    var instances = EM.GetBuffer<BlockInstance>(grid);
                    int index = instances.Length;
                    instances.Add(new BlockInstance { Block = block, Orient = orientation, Origin = new int3(-12 + orientation, 3, 17), Functional = entity, CellsAlive = 1 });
                    EM.SetComponentData(entity, new FunctionalBlock { Grid = grid, InstanceIndex = index });
                }
            }
            Frame();
            var modules = world.GetSingleton<BlockModulesRef>().Value;
            foreach (var instance in EM.GetBuffer<BlockInstance>(grid))
            {
                Assert.That(modules.Value.TryGet(BlockModuleId.WeaponMount, instance.Block, out WeaponMountParams row), Is.True);
                var mount = EM.GetComponentData<WeaponMount>(instance.Functional);
                Assert.That(mount.Initialized, Is.EqualTo(1));
                AssertGeometry(mount, instance, row, .25f);
                var beam = EM.GetComponentData<BeamState>(instance.Functional);
                Assert.That(beam.Width, Is.EqualTo(weapons.Value.Weapons[row.WeaponIndex].BeamWidth));
                Assert.That(beam.Fx, Is.EqualTo(weapons.Value.Weapons[row.WeaponIndex].HitFx));
            }
        }

        [Test]
        public void Reparent_KeepsState()
        {
            var root = SpawnFighter();
            using var query = EM.CreateEntityQuery(ComponentType.ReadOnly<WeaponMount>(), ComponentType.ReadOnly<FunctionalBlock>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            Assert.That(entities.Length, Is.EqualTo(14));
            var weapon = entities[0];
            var functional = EM.GetComponentData<FunctionalBlock>(weapon);
            var instance = EM.GetBuffer<BlockInstance>(root)[functional.InstanceIndex];
            var beforeMount = EM.GetComponentData<WeaponMount>(weapon);
            var beforeState = new WeaponState { Group = 4, Firing = 1, RoundsLeftInBurst = 2, CooldownRemainingTicks = 17.25f,
                CooldownTicks = 39, BurstRemainingTicks = 2.5f, ShotCounter = 77 };
            var beforeBeam = new BeamState { Active = 1, HitSomething = 1, TicksLeft = 7, Width = .77f, Fx = new ContentHash(123),
                MuzzleLocal = new float3(5, 0, 9), EndWorld = new float3(50, 0, 90) };
            var beforeAim = new WeaponAim { Yaw = .2f, DesiredYaw = .1f, FireDirLocal = new float3(.4f, 0, .7f), HasAim = 1, Aligned = 1, InRange = 1, Reserved = 9 };
            EM.SetComponentData(weapon, beforeState); EM.SetComponentData(weapon, beforeBeam); EM.SetComponentData(weapon, beforeAim);
            // A hulk-split test double changes both the grid/index and block placement. No SplitSystem is needed to verify its boundary.
            var rootInstances = EM.GetBuffer<BlockInstance>(root);
            rootInstances[functional.InstanceIndex] = default;
            var hulk = EM.CreateEntity(typeof(VoxelGrid));
            EM.SetComponentData(hulk, new VoxelGrid { CellSize = .75f, Kind = GridKind.Hulk });
            EM.AddBuffer<BlockInstance>(hulk);
            instance.Origin += new int3(100, 7, -40);
            instance.Orient = content.Orientations.Value.AxisTurn(1, 1);
            EM.GetBuffer<BlockInstance>(hulk).Add(instance);
            EM.SetComponentData(weapon, new Parent { Value = hulk });
            EM.SetComponentData(weapon, new FunctionalBlock { Grid = hulk, InstanceIndex = 0 });
            Frame();
            Assert.That(EM.GetComponentData<WeaponState>(weapon), Is.EqualTo(beforeState));
            Assert.That(EM.GetComponentData<BeamState>(weapon), Is.EqualTo(beforeBeam));
            Assert.That(EM.GetComponentData<WeaponAim>(weapon), Is.EqualTo(beforeAim));
            var mount = EM.GetComponentData<WeaponMount>(weapon);
            Assert.That(mount.WeaponIndex, Is.EqualTo(beforeMount.WeaponIndex)); Assert.That(mount.Initialized, Is.EqualTo(1));
            Assert.That(math.distance(mount.MuzzleLocal, beforeMount.MuzzleLocal), Is.GreaterThan(1));
            var modules = world.GetSingleton<BlockModulesRef>().Value;
            Assert.That(modules.Value.TryGet(BlockModuleId.WeaponMount, instance.Block, out WeaponMountParams row), Is.True);
            AssertGeometry(mount, instance, row, .75f);
            // A steady frame must skip geometry as well as preserving state: FunctionalBlock alone owns the change filter.
            mount.PivotLocal = new float3(-999); EM.SetComponentData(weapon, mount);
            Frame();
            Assert.That(EM.GetComponentData<WeaponMount>(weapon), Is.EqualTo(mount));
            Assert.That(EM.GetComponentData<WeaponState>(weapon), Is.EqualTo(beforeState));
            // A stale split reference must be skipped, leaving owned state untouched rather than indexing dead/missing data.
            EM.SetComponentData(weapon, new FunctionalBlock { Grid = Entity.Null, InstanceIndex = 0 });
            Frame(); Assert.That(EM.GetComponentData<WeaponMount>(weapon), Is.EqualTo(mount));
            EM.SetComponentData(weapon, new FunctionalBlock { Grid = hulk, InstanceIndex = int.MaxValue });
            Frame(); Assert.That(EM.GetComponentData<WeaponState>(weapon), Is.EqualTo(beforeState));
            Assert.That(EM.GetComponentData<BeamState>(weapon), Is.EqualTo(beforeBeam));
        }

        void AssertGeometry(in WeaponMount mount, in BlockInstance instance, in WeaponMountParams row, float cellSize)
        {
            ref var block = ref content.BlockDefs.Value.Blocks[instance.Block];
            ref var orientations = ref content.Orientations.Value;
            Assert.That(math.distance(mount.PivotLocal, GridSpaceMath.InstanceCenter(ref block, ref orientations, instance.Orient, instance.Origin, cellSize)), Is.LessThan(1e-5f));
            Assert.That(math.distance(mount.MuzzleLocal, GridSpaceMath.BlockPointToGrid(row.MuzzleOffsetCells, ref orientations, instance.Orient, instance.Origin, cellSize)), Is.LessThan(1e-5f));
            // Independent root-cell-centre + rotated offset oracle catches missing .5 and cell-size conversion.
            var rotation = orientations.Rotations[instance.Orient];
            var expected = ((float3)instance.Origin + .5f + math.rotate(rotation, row.MuzzleOffsetCells)) * cellSize;
            Assert.That(math.distance(mount.MuzzleLocal, expected), Is.LessThan(1e-5f));
            Assert.That(math.distance(mount.ForwardLocal, math.rotate(rotation, new float3(0, 0, 1))), Is.LessThan(1e-5f));
            Assert.That(math.distance(mount.UpLocal, math.rotate(rotation, new float3(0, 1, 0))), Is.LessThan(1e-5f));
            Assert.That(math.dot(mount.ForwardLocal, mount.UpLocal), Is.EqualTo(0).Within(1e-5f));
        }
    }
}
