using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Core.Components;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Simulation.Abilities;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Simulation.Abilities
{
    /// <summary>SPEC-24 ABL-002: hand-authored table fixtures, validation and actual grid-profile conversion.</summary>
    public sealed class AbilityContentTests
    {
        const string Fixtures = "Assets/Game/Tests/EditMode/Simulation/Abilities/Fixtures/";
        readonly List<Object> owned = new List<Object>();
        [TearDown] public void TearDown()
        {
            foreach (var item in owned) Object.DestroyImmediate(item);
            owned.Clear(); TestContentFactory.DestroyAll();
        }
        AbilityDefinition Load(string id)
        {
            var clone = ScriptableObject.CreateInstance<AbilityDefinition>(); owned.Add(clone);
            EditorJsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText(Fixtures + id + ".json"), clone);
            return clone;
        }
        static void Set(AbilityDefinition definition, string field, object value)
            => typeof(AbilityDefinition).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, value);
        ContentCatalog Catalog(params ContentDefinition[] definitions)
        {
            var list = new List<ContentDefinition> { TestContentFactory.Create<BlockDefinition>("block_missing"), TestContentFactory.Create<ItemDefinition>("item_missing") };
            list.AddRange(definitions); var report = new ValidationReport("ABL-002 catalog");
            var catalog = ContentCatalog.Build(list, report); Assert.IsFalse(report.HasErrors, report.ToText()); return catalog;
        }
        [Test] public void Blob_MatchesTable()
        {
            var boost = Load("ability_speed_boost"); var shield = Load("ability_shield_toggle");
            var cue = TestContentFactory.Create<AudioCueDefinition>("cue_ability_test");
            var fx = TestContentFactory.Create<FxDefinition>("fx_ability_test");
            Set(boost, "activationCue", cue); Set(boost, "activationFx", fx);
            var catalog = Catalog(boost, fx, shield, cue); // Deliberately not catalog order.
            using var world = new World("ABL-002 content"); var em = world.EntityManager;
            var created = new List<Entity>(); AbilityContent.CreateSingletons(em, catalog, created);
            Assert.AreEqual(1, created.Count); Assert.IsTrue(em.HasComponent<AbilityDefsRef>(created[0]));
            var blob = em.GetComponentData<AbilityDefsRef>(created[0]).Value;
            try
            {
                ref var root = ref blob.Value;
                Assert.AreEqual(3, root.Abilities.Length);
                ref var none = ref root.Abilities[0];
                Assert.AreEqual(0ul, none.IdHash.Value); Assert.AreEqual(0, none.MaxCharges); Assert.AreEqual(0, none.Mods.Length);
                Assert.AreEqual(1, catalog.IndexOf(shield)); Assert.AreEqual(2, catalog.IndexOf(boost));
                ref var toggle = ref root.Abilities[catalog.IndexOf(shield)];
                Assert.AreEqual(AbilityKind.ShieldToggle, toggle.Kind); Assert.AreEqual(shield.Id.Hash, toggle.IdHash);
                Assert.AreEqual(0, toggle.EnergyCost); Assert.AreEqual(60, toggle.CooldownTicks);
                Assert.AreEqual(1, toggle.MaxCharges); Assert.AreEqual(0, toggle.ChargeRechargeTicks); Assert.AreEqual(0u, toggle.DurationTicks);
                Assert.AreEqual(.3f, toggle.ShieldEnableThreshold); Assert.AreEqual(0, toggle.Mods.Length);
                Assert.AreEqual(0ul, toggle.ActivationCue.Value); Assert.AreEqual(0ul, toggle.ActivationFx.Value);
                ref var speed = ref root.Abilities[catalog.IndexOf(boost)];
                Assert.AreEqual(AbilityKind.StatBoost, speed.Kind); Assert.AreEqual(boost.Id.Hash, speed.IdHash);
                Assert.AreEqual(15, speed.EnergyCost); Assert.AreEqual(30, speed.CooldownTicks);
                Assert.AreEqual(2, speed.MaxCharges); Assert.AreEqual(180, speed.ChargeRechargeTicks); Assert.AreEqual(60u, speed.DurationTicks);
                Assert.AreEqual(2, speed.Mods.Length); Assert.AreEqual(StatId.MaxSpeed, speed.Mods[0].Stat); Assert.AreEqual(1.8f, speed.Mods[0].Multiplier);
                Assert.AreEqual(StatId.Thrust, speed.Mods[1].Stat); Assert.AreEqual(2, speed.Mods[1].Multiplier);
                Assert.AreEqual(cue.Id.Hash, speed.ActivationCue); Assert.AreEqual(fx.Id.Hash, speed.ActivationFx); Assert.AreEqual(0, speed.Reserved);
                Assert.Throws<InvalidOperationException>(() => AbilityContent.CreateSingletons(em, catalog, created));
                Assert.AreEqual(1, created.Count, "Duplicate calls do not leak another singleton/blob");
            }
            finally { em.DestroyEntity(created[0]); blob.Dispose(); }
            created.Clear();
            // Fractional cooldown/recharge stay float ticks; duration uses the canonical round-half-up conversion.
            Set(boost, "cooldownSeconds", .025f); Set(boost, "chargeRechargeSeconds", .025f); Set(boost, "durationSeconds", .025f);
            AbilityContent.CreateSingletons(em, catalog, created); blob = em.GetComponentData<AbilityDefsRef>(created[0]).Value;
            try
            {
                ref var speed = ref blob.Value.Abilities[catalog.IndexOf(boost)];
                Assert.AreEqual(1.5f, speed.CooldownTicks); Assert.AreEqual(1.5f, speed.ChargeRechargeTicks); Assert.AreEqual(2u, speed.DurationTicks);
            }
            finally { em.DestroyEntity(created[0]); blob.Dispose(); }
            created.Clear(); AbilityContent.CreateSingletons(em, Catalog(), created);
            blob = em.GetComponentData<AbilityDefsRef>(created[0]).Value;
            try { Assert.AreEqual(1, blob.Value.Abilities.Length); Assert.AreEqual(0ul, blob.Value.Abilities[0].IdHash.Value); }
            finally { em.DestroyEntity(created[0]); blob.Dispose(); }
        }
        void Error(string id, string code, Action<AbilityDefinition> edit)
        {
            var definition = Load(id); edit(definition); var report = new ValidationReport("ABL-002 validation");
            definition.Validate(report.For("test:" + id), null);
            Assert.IsTrue(report.HasErrors); bool found = false;
            foreach (var entry in report.Entries)
            { found |= entry.Code == code; Assert.AreNotEqual("ABL-E004", entry.Code, "Retired code is never emitted"); }
            Assert.IsTrue(found, report.ToText());
        }
        [Test] public void Validation_Codes()
        {
            foreach (string id in new[] { "ability_speed_boost", "ability_shield_toggle" })
            {
                var definition = Load(id); var report = new ValidationReport("Valid fixture");
                definition.Validate(report.For(id), null); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            }
            Error("ability_speed_boost", "ABL-E001", d => Set(d, "statModifiers", null));
            Error("ability_speed_boost", "ABL-E001", d => Set(d, "statModifiers", new AbilityStatMod[5]));
            Error("ability_speed_boost", "ABL-E001", d => Set(d, "statModifiers", new[] { new AbilityStatMod { Stat = (StatId)255, Multiplier = 1 } }));
            Error("ability_speed_boost", "ABL-E001", d => Set(d, "statModifiers", new[] { new AbilityStatMod { Stat = StatId.Thrust, Multiplier = float.NaN } }));
            Error("ability_speed_boost", "ABL-E001", d => Set(d, "statModifiers", new[] { new AbilityStatMod { Stat = StatId.Thrust, Multiplier = 1 }, new AbilityStatMod { Stat = StatId.Thrust, Multiplier = 2 } }));
            Error("ability_speed_boost", "ABL-E002", d => Set(d, "durationSeconds", 0f));
            Error("ability_speed_boost", "ABL-E002", d => Set(d, "durationSeconds", .001f));
            Error("ability_speed_boost", "ABL-E003", d => Set(d, "chargeRechargeSeconds", 0f));
            Error("ability_speed_boost", "ABL-E003", d => Set(d, "maxCharges", 10));
            Error("ability_shield_toggle", "ABL-E005", d => Set(d, "durationSeconds", 1f));
            Error("ability_shield_toggle", "ABL-E006", d => Set(d, "energyCost", -1f));
            Error("ability_shield_toggle", "ABL-E006", d => Set(d, "cooldownSeconds", float.PositiveInfinity));
            Error("ability_shield_toggle", "ABL-E006", d => Set(d, "cooldownSeconds", float.MaxValue));
            Error("ability_shield_toggle", "ABL-E006", d => Set(d, "shieldEnableThreshold", float.NaN));
            Error("ability_shield_toggle", "ABL-E006", d => Set(d, "kind", (AbilityKind)255));
            var bad = Load("ability_speed_boost"); Set(bad, "durationSeconds", 0f);
            using var world = new World("ABL-002 invalid content"); var created = new List<Entity>();
            Assert.Throws<ContentValidationException>(() => AbilityContent.CreateSingletons(world.EntityManager, Catalog(bad), created));
            using var query = world.EntityManager.CreateEntityQuery(typeof(AbilityDefsRef));
            Assert.IsTrue(query.IsEmpty); Assert.AreEqual(0, created.Count, "Validation precedes allocation/publication");
        }
        [Test] public void GridSetup_AddsComponents()
        {
            using var content = new VoxelTestContent();
            using var library = new BlueprintLibrary(content.Service.Catalog, content.Content);
            var registry = VoxelStructureRegistry.CreateRuntime(library);
            using var source = new VoxelStructure();
            source.SetBrickUniform(new int3(0), Cell.Palette(source.FindOrAddPalette(new PaletteEntry { Block = content.Index("block_hull") })));
            var world = new World("ABL-002 profiles"); var em = world.EntityManager;
            try
            {
                content.Service.CreateSingletons(em);
                em.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
                em.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
                em.CreateEntity(typeof(GameplayActive)); var spawn = world.CreateSystem<GridSpawnSystem>();
                using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<RootComponentRegistration>()))
                {
                    var registrations = em.GetBuffer<RootComponentRegistration>(query.GetSingletonEntity());
                    int initial = registrations.Length; AbilityGridSetup.Register(registrations);
                    Assert.AreEqual(initial + 10, registrations.Length); Assert.AreEqual(2, AbilityGridSetup.RootTypes.Length);
                }
                int handle = registry.Register(source);
                for (GridProfile profile = GridProfile.None; profile <= GridProfile.Dummy; profile++)
                {
                    var root = GridSpawnApi.RequestSpawn(em, registry, handle, new GridSpawnParams
                    { PersistentId = (ulong)profile + 1, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender, Profile = profile, Motion = GridMotion.Dynamic });
                    spawn.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                    bool combat = profile != GridProfile.None;
                    Assert.AreEqual(combat, em.HasComponent<AbilitySlot>(root), profile.ToString());
                    Assert.AreEqual(combat, em.HasComponent<AbilityState>(root), profile.ToString());
                    if (combat)
                    {
                        Assert.AreEqual(0, em.GetBuffer<AbilitySlot>(root).Length);
                        Assert.AreEqual(0u, em.GetComponentData<AbilityState>(root).SyncedRevision);
                        Assert.AreEqual(0, em.GetComponentData<AbilityState>(root).SlotCount);
                    }
                }
            }
            finally { world.Dispose(); Object.DestroyImmediate(registry); }
        }
    }
}
