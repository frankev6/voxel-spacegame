using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Migration;
using Game.Editor.Migration.Converters;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>MIG-010: real export values/grants, byte-stable reruns and protected manifest ownership (SPEC-72 §4.6).</summary>
    public sealed class AbilityConverterTests
    {
        const string Root = "Assets/Game/Content/_AbilityMigrationTest";
        LegacyContentIndex index;
        MigrationManifest manifest;
        ContentCatalog catalog;
        bool ownsRoot;

        [SetUp] public void SetUp()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root) || Directory.Exists(Root) || File.Exists(Root + ".meta"), "Never remove existing user content.");
            ownsRoot = true;
            manifest = new MigrationManifest();
            var report = new ValidationReport("Ability migration source");
            index = LegacyContentIndex.Load(Path.GetFullPath("Import/Legacy"), report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            var blocks = Context();
            new BlockConverter().Convert(blocks);
            Assert.IsFalse(blocks.Report.HasErrors, blocks.Report.ToText());
            var definitions = blocks.Writer.Outcomes.Select(o => AssetDatabase.LoadAssetAtPath<BlockDefinition>(o.AssetPath)).Cast<ContentDefinition>().ToList();
            definitions.Add(AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"));
            definitions.Add(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"));
            catalog = ContentCatalog.Build(definitions, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
        }

        [TearDown] public void Clean()
        {
            if (ownsRoot) AssetDatabase.DeleteAsset(Root);
            ownsRoot = false;
        }

        [Test]
        public void RealExport_ValuesAndGrants()
        {
            var converter = new AbilityConverter();
            Assert.AreEqual("Abilities", converter.Name); Assert.AreEqual(210, converter.Order);
            var context = Context(); converter.Convert(context);
            Assert.IsFalse(context.Report.HasErrors, context.Report.ToText());
            Assert.AreEqual(2, AssetDatabase.FindAssets("t:AbilityDefinition", new[] { Root }).Length);
            Assert.AreEqual(2, context.Writer.Outcomes.Count(o => o.Outcome == WriteOutcome.Created));
            Assert.AreEqual(4, context.Writer.Outcomes.Count(o => o.Outcome == WriteOutcome.Updated));

            var speed = Ability("ability_speed_boost"); var shield = Ability("ability_shield_toggle");
            Assert.NotNull(speed); Assert.NotNull(shield);
            Assert.AreEqual("ability_speed_boost", speed.IdString); Assert.AreEqual("Speed Boost", speed.DisplayName);
            Assert.AreEqual("", speed.Description); Assert.AreEqual("spritetest2_transparent_25", speed.Icon.name);
            Assert.AreEqual(AbilityKind.StatBoost, speed.Kind); Assert.AreEqual(15f, speed.EnergyCost);
            Assert.AreEqual(.5f, speed.CooldownSeconds); Assert.AreEqual(2, speed.MaxCharges);
            Assert.AreEqual(3f, speed.ChargeRechargeSeconds); Assert.AreEqual(1f, speed.DurationSeconds);
            Assert.AreEqual(0f, speed.ShieldEnableThreshold);
            Assert.AreEqual(2, speed.StatModifiers.Count);
            Assert.AreEqual(StatId.MaxSpeed, speed.StatModifiers[0].Stat); Assert.AreEqual(1.8f, speed.StatModifiers[0].Multiplier);
            Assert.AreEqual(StatId.Thrust, speed.StatModifiers[1].Stat); Assert.AreEqual(2f, speed.StatModifiers[1].Multiplier);
            Assert.AreEqual("ability_shield_toggle", shield.IdString); Assert.AreEqual("Shield Toggle", shield.DisplayName);
            Assert.AreEqual("", shield.Description); Assert.AreEqual("spritetest2_transparent_40", shield.Icon.name);
            Assert.AreEqual(AbilityKind.ShieldToggle, shield.Kind); Assert.AreEqual(0f, shield.EnergyCost);
            Assert.AreEqual(1f, shield.CooldownSeconds); Assert.AreEqual(1, shield.MaxCharges);
            Assert.AreEqual(0f, shield.ChargeRechargeSeconds); Assert.AreEqual(0f, shield.DurationSeconds);
            Assert.AreEqual(.3f, shield.ShieldEnableThreshold); Assert.AreEqual(0, shield.StatModifiers?.Count ?? 0);
            foreach (var ability in new[] { speed, shield })
            {
                Assert.IsNull(ability.ActivationCue); Assert.IsNull(ability.ActivationFx);
                var validation = new ValidationReport("Ability"); string path = AssetDatabase.GetAssetPath(ability);
                ability.Validate(validation.For(path), new ContentValidationContext(path, true, _ => true, catalog));
                Assert.IsFalse(validation.HasErrors, validation.ToText());
            }
            Assert.AreEqual(1, context.Report.Entries.Count(e => e.Code == "MIG-I091" && e.Message.Contains("Description")));
            string[] thrusters = { "block_thruster_small", "block_thruster_medium", "block_thruster_large" };
            foreach (string id in thrusters) CollectionAssert.AreEqual(new[] { speed }, Block(id).GrantedAbilities, id);
            CollectionAssert.AreEqual(new[] { shield }, Block("block_shield_small").GrantedAbilities);

            var paths = context.Writer.Outcomes.Select(o => o.AssetPath).Distinct().ToArray();
            var snapshots = paths.ToDictionary(p => p, p => (Bytes: File.ReadAllBytes(p), Guid: AssetDatabase.AssetPathToGUID(p)));
            foreach (string path in paths)
            {
                Assert.IsTrue(manifest.TryGetHash(path, out ulong hash));
                Assert.AreEqual(StableHash.Hash64(File.ReadAllBytes(path)), hash, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var again = Context(); converter.Convert(again);
            Assert.IsFalse(again.Report.HasErrors, again.Report.ToText());
            Assert.AreEqual(2, again.Writer.Outcomes.Count);
            Assert.IsTrue(again.Writer.Outcomes.All(o => o.Outcome == WriteOutcome.Unchanged));
            Assert.AreEqual(4, again.Report.Entries.Count(e => e.Code == "MIG-I012"));
            foreach (var pair in snapshots)
            {
                CollectionAssert.AreEqual(pair.Value.Bytes, File.ReadAllBytes(pair.Key), pair.Key);
                Assert.AreEqual(pair.Value.Guid, AssetDatabase.AssetPathToGUID(pair.Key), pair.Key);
            }

            string speedPath = AssetDatabase.GetAssetPath(speed);
            string smallPath = AssetDatabase.GetAssetPath(Block("block_thruster_small"));
            Assert.IsTrue(manifest.TryGetHash(speedPath, out ulong ownedSpeedHash));
            Assert.IsTrue(manifest.TryGetHash(smallPath, out ulong ownedSmallHash));
            using (var so = new SerializedObject(speed))
            { so.FindProperty("energyCost").floatValue = 99f; so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(speed); }
            var small = Block("block_thruster_small");
            using (var so = new SerializedObject(small))
            {
                so.FindProperty("cellHp").floatValue = 777f;
                so.FindProperty("grantedAbilities").arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(small);
            }
            var medium = Block("block_thruster_medium");
            using (var so = new SerializedObject(medium))
            {
                so.FindProperty("grantedAbilities").GetArrayElementAtIndex(0).objectReferenceValue = shield;
                so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(medium);
            }
            byte[] editedSpeed = File.ReadAllBytes(speedPath);
            string mediumPath = AssetDatabase.GetAssetPath(medium);
            byte[] editedMedium = File.ReadAllBytes(mediumPath);
            var protectedRun = Context(); converter.Convert(protectedRun);
            Assert.IsFalse(protectedRun.Report.HasErrors, protectedRun.Report.ToText());
            Assert.AreEqual(1, protectedRun.Writer.Outcomes.Count(o => o.Outcome == WriteOutcome.SkippedHandEdited));
            CollectionAssert.AreEqual(editedSpeed, File.ReadAllBytes(speedPath), "hand-edited ability tuning is preserved");
            CollectionAssert.AreEqual(editedMedium, File.ReadAllBytes(mediumPath), "a nonempty authored grant array is preserved");
            Assert.AreEqual(99f, speed.EnergyCost); Assert.AreEqual(777f, small.CellHp);
            CollectionAssert.AreEqual(new[] { speed }, small.GrantedAbilities, "an empty grant can be filled without taking ownership of tuning");
            Assert.IsTrue(manifest.TryGetHash(speedPath, out ulong retainedSpeedHash)); Assert.AreEqual(ownedSpeedHash, retainedSpeedHash);
            Assert.IsTrue(manifest.TryGetHash(smallPath, out ulong retainedSmallHash)); Assert.AreEqual(ownedSmallHash, retainedSmallHash);
            Assert.AreNotEqual(retainedSpeedHash, StableHash.Hash64(File.ReadAllBytes(speedPath)));
            Assert.AreNotEqual(retainedSmallHash, StableHash.Hash64(File.ReadAllBytes(smallPath)));
        }

        LegacyMigrationContext Context()
        {
            var report = new ValidationReport("MIG-010");
            return new LegacyMigrationContext(index, LegacyIdMap.Default, new LegacyAssetWriter(Root, manifest, false, report), report, false,
                Path.GetFullPath("Import/Legacy")) { Catalog = catalog };
        }
        static AbilityDefinition Ability(string id) => AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Root + "/Abilities/" + id + ".asset");
        static BlockDefinition Block(string id) => AssetDatabase.LoadAssetAtPath<BlockDefinition>(Root + "/Blocks/" + id + ".asset");
    }
}
