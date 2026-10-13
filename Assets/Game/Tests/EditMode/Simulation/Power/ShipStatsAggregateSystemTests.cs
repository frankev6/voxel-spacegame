using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation;
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
using Unity.Physics;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>Eight PWR-006 acceptance checks against the real Fighter spawn and shipped module blobs.</summary>
    public sealed class ShipStatsAggregateSystemTests
    {
        EcsTestWorld world;
        ContentService content;
        BlueprintLibrary library;
        VoxelStructureRegistry registry;
        BlobAssetReference<WeaponDefsBlob> weapons;
        FactionRelations factions;
        Entity root;
        EntityManager Em => world.EntityManager;
        [SetUp] public void SetUp()
        {
            content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
            library = new BlueprintLibrary(content.Catalog, new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations });
            registry = VoxelStructureRegistry.CreateRuntime(library);
            world = EcsTestWorld.Create(typeof(GridSpawnSystem), typeof(PowerRegistrationSystem), typeof(TestConsumerInitSystem), typeof(PowerInitSystem), typeof(ShipStatsAggregateSystem));
            content.CreateSingletons(Em); world.AddSingleton(new VoxelStructureRegistryRef { Value = registry });
            world.AddSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
            world.AddSingleton(PowerTuning.Defaults); world.AddGameplayActive();
            WeaponContent.CreateSingletons(Em, content.Catalog, new List<Entity>());
            using var w = Em.CreateEntityQuery(typeof(WeaponDefsRef)); weapons = w.GetSingleton<WeaponDefsRef>().Value;
            using var f = Em.CreateEntityQuery(typeof(FactionRelations)); factions = f.GetSingleton<FactionRelations>();
            root = GridSpawnApi.RequestSpawnBlueprint(Em, new ContentId("ship_fighter").Hash, new GridSpawnParams {
                PersistentId = 1, Pose = RigidTransform.identity, Profile = GridProfile.PlayerShip, Motion = GridMotion.Dynamic, Flags = GridSpawnFlags.NoRender });
            Frame();
        }
        [TearDown] public void TearDown()
        {
            world?.Dispose(); if (weapons.IsCreated) weapons.Dispose(); factions.Dispose();
            if (registry != null) Object.DestroyImmediate(registry); library?.Dispose(); content?.Dispose();
        }
        void Frame() { world.StepFrame(EcsTestWorld.TickSeconds); Em.CompleteAllTrackedJobs(); }
        ShipStats Stats => Em.GetComponentData<ShipStats>(root);
        void DestroyWeapon()
        {
            int index = -1;
            foreach (var consumer in Em.GetBuffer<PowerConsumer>(root))
                if (consumer.Module == BlockModuleId.WeaponMount) { index = consumer.InstanceIndex; break; }
            Assert.GreaterOrEqual(index, 0);
            var instances = Em.GetBuffer<BlockInstance>(root); var instance = instances[index];
            instance.Flags |= BlockInstanceFlags.Destroyed; instances[index] = instance;
        }
        [Test] public void Spawn_FullRecompute()
        {
            int count = 0;
            foreach (var instance in Em.GetBuffer<BlockInstance>(root))
                if (instance.Block != 0 && (instance.Flags & BlockInstanceFlags.Destroyed) == 0 && content.BlockDefs.Value.Blocks[instance.Block].Kind == BlockKind.Functional) count++;
            Assert.Greater(count, 0); Assert.AreEqual(count, Stats.AliveFunctionalCount);
            Assert.AreEqual(1u, Stats.FunctionalRevision);
            var power = Em.GetComponentData<PowerStats>(root);
            Assert.AreEqual(40, power.Generation); Assert.AreEqual(200, power.Capacity); Assert.AreEqual(1u, power.SourceRevision);
            var grid = Em.GetComponentData<VoxelGrid>(root);
            Assert.AreEqual((float3)grid.CellMin * grid.CellSize, Stats.BoundsMin);
            Assert.AreEqual(((float3)grid.CellMax + 1) * grid.CellSize, Stats.BoundsMax);
        }
        [Test] public void WeaponDestroyed_IncrementsRevision()
        {
            var before = Stats; DestroyWeapon(); Frame();
            Assert.AreEqual(before.FunctionalRevision + 1, Stats.FunctionalRevision);
            Assert.AreEqual(before.AliveFunctionalCount - 1, Stats.AliveFunctionalCount);
            Frame(); Assert.AreEqual(before.FunctionalRevision + 1, Stats.FunctionalRevision);
        }
        [Test] public void HullDamageOnly_NoRecompute()
        {
            var before = Stats; bool changed = false;
            foreach (var slot in Em.GetBuffer<BrickSlot>(root))
            {
                if (!Em.HasBuffer<BrickCells>(slot.Brick)) continue;
                var cells = Em.GetBuffer<BrickCells>(slot.Brick);
                for (int i = 0; i < cells.Length; i++)
                {
                    var cell = cells[i].Value;
                    if (!Structural(cell)) continue;
                    cell.Hp = 128; cells[i] = new BrickCells { Value = cell }; changed = true; break;
                }
                if (changed) break;
                if (!Em.HasBuffer<BrickSparseCells>(slot.Brick)) continue;
                var sparse = Em.GetBuffer<BrickSparseCells>(slot.Brick);
                for (int i = 0; i < sparse.Length; i++)
                {
                    var entry = sparse[i]; if (!Structural(entry.Value)) continue;
                    entry.Value.Hp = 128; sparse[i] = entry; changed = true; break;
                }
                if (changed) break;
            }
            Assert.IsTrue(changed, "Fixture must damage a real structural hull cell.");
            var grid = Em.GetComponentData<VoxelGrid>(root); grid.Revision++; Em.SetComponentData(root, grid);
            Frame(); Assert.AreEqual(before.FunctionalRevision, Stats.FunctionalRevision); Assert.AreEqual(before.FunctionalSignature, Stats.FunctionalSignature);
        }
        bool Structural(Cell cell)
        {
            if (cell.IsAir) return false;
            ushort block = cell.IsInstance ? Em.GetBuffer<BlockInstance>(root)[cell.InstanceIndex].Block : Em.GetBuffer<PaletteEntry>(root)[cell.PaletteIndex].Block;
            return block != 0 && content.BlockDefs.Value.Blocks[block].Kind == BlockKind.Structural;
        }
        [Test] public void MassChange_CheapUpdateOnly()
        {
            uint revision = Stats.FunctionalRevision;
            if (!Em.HasComponent<PhysicsMass>(root)) Em.AddComponentData(root, new PhysicsMass { InverseMass = .01f });
            else Em.SetComponentData(root, new PhysicsMass { InverseMass = .01f });
            Frame(); float initial = Stats.InitialMass;
            Assert.AreEqual(100, Stats.Mass, .001f);
            Em.SetComponentData(root, new PhysicsMass { InverseMass = .02f }); Frame();
            Assert.AreEqual(50, Stats.Mass, .001f); Assert.AreEqual(50 / initial, Stats.MassFraction, .0001f);
            Assert.AreEqual(revision, Stats.FunctionalRevision);
        }
        [Test] public void Modifier_AppliesMultiplier()
        {
            uint revision = Stats.FunctionalRevision;
            Em.GetBuffer<ShipStatModifier>(root).Add(new ShipStatModifier { Stat = StatId.Thrust, Multiplier = 3, UntilTick = uint.MaxValue }); Frame();
            var multipliers = Em.GetComponentData<ShipStatMultipliers>(root);
            for (int i = 0; i <= (int)StatId.TurnRate; i++) Assert.AreEqual(i == (int)StatId.Thrust ? 3 : 1, multipliers.Get((StatId)i));
            Assert.AreEqual(revision, Stats.FunctionalRevision);
        }
        [Test] public void PowerStats_FollowRevision()
        {
            float draw = Em.GetComponentData<PowerStats>(root).DrawAtFullDemand;
            DestroyWeapon(); Frame(); var power = Em.GetComponentData<PowerStats>(root);
            Assert.AreEqual(Stats.FunctionalRevision, power.SourceRevision);
            Assert.AreEqual(Stats.FunctionalRevision, Em.GetComponentData<ShieldStats>(root).SourceRevision);
            Assert.Less(power.DrawAtFullDemand, draw); Assert.AreEqual(40, power.Generation); Assert.AreEqual(200, power.Capacity);
        }
        [Test] public void Dormant_Skipped()
        {
            var before = Stats;
            if (!Em.HasComponent<Dormant>(root)) Em.AddComponent<Dormant>(root);
            Em.SetComponentEnabled<Dormant>(root, true); DestroyWeapon(); Frame(); Assert.AreEqual(before, Stats);
            Em.SetComponentEnabled<Dormant>(root, false); Frame(); Assert.AreEqual(before.FunctionalRevision + 1, Stats.FunctionalRevision);
        }
        [Test] public void Hulk_Skipped()
        {
            var before = Stats; var power = Em.GetComponentData<PowerStats>(root);
            Em.AddComponent<Hulk>(root); DestroyWeapon(); Frame(); Assert.AreEqual(before, Stats); Assert.AreEqual(power, Em.GetComponentData<PowerStats>(root));
        }
    }
}
