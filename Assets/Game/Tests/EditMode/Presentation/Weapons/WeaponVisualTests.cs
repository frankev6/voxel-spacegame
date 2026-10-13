using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Presentation.Weapons;
using Game.Simulation.Common;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

namespace Game.Tests.EditMode.Presentation.Weapons
{
    /// <summary>SPEC-21 WPN-011: real WorldGroup/transform jobs over existing approved weapon definitions.</summary>
    public sealed class WeaponVisualTests
    {
        ContentService content;
        BlobAssetReference<WeaponDefsBlob> weapons;
        EcsTestWorld world;
        EntityManager EM => world.EntityManager;

        [SetUp] public void SetUp()
        {
            content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
            weapons = WeaponContent.BuildBlob(content.Catalog);
            world = EcsTestWorld.Create(typeof(WeaponVisualSyncSystem), typeof(ParentSystem), typeof(LocalToWorldSystem));
            content.CreateSingletons(EM);
            world.AddSingleton(new WeaponDefsRef { Value = weapons });
            world.AddGameplayActive();
        }
        [TearDown] public void TearDown()
        {
            world?.Dispose(); if (weapons.IsCreated) weapons.Dispose(); weapons = default; content?.Dispose();
        }
        void Frame() => world.StepFrame(EcsTestWorld.TickSeconds);
        Entity Grid(float size, LocalTransform pose)
        {
            var grid = EM.CreateEntity(typeof(VoxelGrid), typeof(LocalTransform), typeof(LocalToWorld));
            EM.SetComponentData(grid, new VoxelGrid { CellSize = size });
            EM.SetComponentData(grid, pose); EM.AddBuffer<BlockInstance>(grid);
            return grid;
        }
        Entity Weapon(Entity grid, ushort block, byte orientation, int3 origin, float yaw)
        {
            var entity = EM.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount), typeof(WeaponAim), typeof(LocalTransform), typeof(LocalToWorld), typeof(Parent));
            var instances = EM.GetBuffer<BlockInstance>(grid);
            int index = instances.Length;
            instances.Add(new BlockInstance { Block = block, Orient = orientation, Origin = origin, Functional = entity });
            EM.SetComponentData(entity, new FunctionalBlock { Grid = grid, InstanceIndex = index });
            EM.SetComponentData(entity, new Parent { Value = grid });
            EM.SetComponentData(entity, Geometry(grid, block, orientation, origin));
            EM.SetComponentData(entity, new WeaponAim { Yaw = yaw });
            EM.SetComponentData(entity, LocalTransform.FromPositionRotationScale(GridSpaceMath.CellCenter(origin, EM.GetComponentData<VoxelGrid>(grid).CellSize),
                content.Orientations.Value.Rotations[orientation], EM.GetComponentData<VoxelGrid>(grid).CellSize));
            return entity;
        }
        WeaponMount Geometry(Entity grid, ushort block, byte orientation, int3 origin)
        {
            ref var definition = ref content.BlockDefs.Value.Blocks[block];
            ref var rotations = ref content.Orientations.Value;
            var modules = world.GetSingleton<BlockModulesRef>().Value;
            Assert.IsTrue(modules.Value.TryGet(BlockModuleId.WeaponMount, block, out WeaponMountParams row));
            return new WeaponMount
            {
                WeaponIndex = row.WeaponIndex, Initialized = 1,
                PivotLocal = GridSpaceMath.InstanceCenter(ref definition, ref rotations, orientation, origin, EM.GetComponentData<VoxelGrid>(grid).CellSize),
                UpLocal = rotations.Rotate(new float3(0, 1, 0), orientation)
            };
        }
        void AssertPose(Entity entity, ushort block)
        {
            var functional = EM.GetComponentData<FunctionalBlock>(entity);
            var instance = EM.GetBuffer<BlockInstance>(functional.Grid)[functional.InstanceIndex];
            var actual = EM.GetComponentData<LocalTransform>(entity);
            var mount = EM.GetComponentData<WeaponMount>(entity);
            float size = EM.GetComponentData<VoxelGrid>(functional.Grid).CellSize;
            float yaw = EM.GetComponentData<WeaponAim>(entity).Yaw;
            ref var definition = ref content.BlockDefs.Value.Blocks[block];
            float3 centreInMesh = ((float3)definition.BoundsMin + (float3)definition.BoundsMax) * .5f;
            Assert.Less(math.distance(actual.TransformPoint(centreInMesh), mount.PivotLocal), 1e-4f, "The rotated footprint centre must stay exactly at the pivot.");
            Assert.AreEqual(size, actual.Scale);
            quaternion rest = content.Orientations.Value.Rotations[instance.Orient];
            // Rodrigues' formula is an independent direction reference, including wall/ceiling orientations.
            float3 forward = math.rotate(rest, new float3(0, 0, 1));
            float3 up = mount.UpLocal;
            float3 expectedForward = forward * math.cos(yaw) + math.cross(up, forward) * math.sin(yaw) + up * math.dot(up, forward) * (1 - math.cos(yaw));
            Assert.Less(math.distance(expectedForward, math.rotate(actual.Rotation, new float3(0, 0, 1))), 1e-4f);
            Assert.Less(math.distance(up, math.rotate(actual.Rotation, new float3(0, 1, 0))), 1e-4f);
            var parent = EM.GetComponentData<LocalTransform>(functional.Grid);
            Assert.Less(math.distance(parent.TransformPoint(actual.Position), EM.GetComponentData<LocalToWorld>(entity).Position), 1e-4f,
                "The visual write must precede actual hierarchy propagation.");
        }

        [Test] public void Turret_RotatesAboutPivot()
        {
            ushort turret = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("weapon_plasma_turret_medium")));
            ushort fixedBlock = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("weapon_plasma_turret_small_fixed")));
            var grid = Grid(.5f, LocalTransform.FromPositionRotation(new float3(11, 3, -7), quaternion.RotateY(.7f)));
            var entities = new List<Entity>();
            for (byte orientation = 0; orientation < 24; orientation++)
                entities.Add(Weapon(grid, turret, orientation, new int3(3, -2, 5), .6f));
            var fixedWeapon = Weapon(grid, fixedBlock, 0, new int3(-3, 0, 2), 1.2f);
            Assert.AreEqual(1, weapons.Value.Weapons[EM.GetComponentData<WeaponMount>(fixedWeapon).WeaponIndex].IsFixed);
            var fixedPose = EM.GetComponentData<LocalTransform>(fixedWeapon);
            Frame(); Frame(); // Let ParentSystem establish PreviousParent/Child before testing steady-state change filters.
            foreach (var entity in entities) AssertPose(entity, turret);
            Assert.AreEqual(fixedPose, EM.GetComponentData<LocalTransform>(fixedWeapon), "Fixed weapons never move, even with a nonzero test yaw.");

            // No accumulated rotation: each changed yaw is absolute relative to the authored spawn pose.
            foreach (var entity in entities) EM.SetComponentData(entity, new WeaponAim { Yaw = -.35f });
            EM.SetComponentData(fixedWeapon, new WeaponAim { Yaw = -.35f });
            Frame();
            foreach (var entity in entities) AssertPose(entity, turret);
            Assert.AreEqual(fixedPose, EM.GetComponentData<LocalTransform>(fixedWeapon));

            var chosen = entities[0];
            var markerPose = LocalTransform.FromPosition(new float3(91, 92, 93));
            EM.SetComponentData(chosen, markerPose); Frame();
            Assert.AreEqual(markerPose, EM.GetComponentData<LocalTransform>(chosen), "Unchanged aim/mount chunks are skipped.");

            // Split-style reparenting preserves yaw but changes origin/cell scale and refreshes mount geometry.
            var newGrid = Grid(.25f, LocalTransform.FromPositionRotation(new float3(-10, 2, 8), quaternion.RotateY(-.4f)));
            var newOrigin = new int3(-8, 2, 4);
            EM.GetBuffer<BlockInstance>(newGrid).Add(new BlockInstance { Block = turret, Orient = 0, Origin = newOrigin, Functional = chosen });
            EM.SetComponentData(chosen, new FunctionalBlock { Grid = newGrid, InstanceIndex = 0 });
            EM.SetComponentData(chosen, new Parent { Value = newGrid });
            EM.SetComponentData(chosen, Geometry(newGrid, turret, 0, newOrigin));
            Frame(); AssertPose(chosen, turret);
            Assert.AreEqual(-.35f, EM.GetComponentData<WeaponAim>(chosen).Yaw);
            Assert.AreEqual(fixedPose, EM.GetComponentData<LocalTransform>(fixedWeapon));
        }
    }
}
