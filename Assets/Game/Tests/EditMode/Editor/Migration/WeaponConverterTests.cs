using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Migration;
using Game.Editor.Migration.Converters;
using Game.Editor.Migration.Dto;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>MIG-009: independent SPEC-21 oracle, strict legacy identity, and real protected serialized links.</summary>
    public sealed class WeaponConverterTests
    {
        const string Root = "Assets/Game/Content/_WeaponMigrationTest";
        const string NumericFields = "damage|penetrationCells|explosionRadius|explosionDamage|impulsePerHit|fireRate|burstCount|burstInterval|pelletsPerRound|spreadDeg|projectileSpeed|projectileRadius|lifetimeMin|lifetimeMax|homingTurnRateDeg|beamRange|beamPulseSeconds|beamWidth|traverseHalfArcDeg|traverseRateDeg|maxElevationDeg|fireConeDeg|powerDraw|defaultFireGroups";
        // SPEC-21 §4.9 plus its common values; every numeric serialized field, independent of the converter table.
        const string Rows = @"wpn_plasma_turret_small|60|1|0|0|120|2|1|0|1|2|400|0.1|1.5|1.5|0|0|0.5|0.3|5|360|20|3|1.5|1
wpn_plasma_turret_small_fixed|60|1|0|0|120|2.5|1|0|1|0.5|400|0.1|1.5|1.5|0|0|0.5|0.3|0|0|20|3|1.5|1
wpn_plasma_turret_medium|70|2|0|0|140|1.5|3|0.05|1|0.5|450|0.1|1.6|1.6|0|0|0.5|0.3|90|180|20|3|4|1
wpn_small_beam_turret|150|1|0|0|300|0.6|1|0|1|0|0|0.1|0|0|0|450|0.5|0.3|90|180|20|3|4|2
wpn_fixed_beam_turret_small|180|1|0|0|360|0.6|1|0|1|0|0|0.1|0|0|0|450|0.5|0.3|0|0|20|3|4|2
wpn_railgun_medium|250|6|0|0|500|0.3|4|0.1|1|0.5|1200|0.1|1|1|0|0|0.5|0.3|30|90|20|3|8|1
wpn_ark_lance_medium|900|2|0|0|1800|0.1|1|0|1|0|0|0.1|0|0|0|900|1|0.6|35|60|20|3|10|2
wpn_flak_cannon_medium|0|1|2.5|60|0|1|1|0|8|6|300|0.2|0.5|1.5|0|0|0.5|0.3|0|0|20|3|5|4
wpn_missile_launcher_small|0|1|3|200|0|0.3|6|0.05|1|2|180|0.3|5|5|90|0|0.5|0.3|0|0|20|3|2|4";
        LegacyContentIndex index;
        MigrationManifest manifest;
        Dictionary<string, BlockDefinition> blocks;
        ContentCatalog catalog;
        bool ownsRoot;

        [SetUp] public void SetUp()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root) || Directory.Exists(Root), "Never remove existing user content.");
            ownsRoot = true; Directory.CreateDirectory(Root + "/Blocks"); Directory.CreateDirectory(Root + "/Weapons"); AssetDatabase.Refresh();
            manifest = new MigrationManifest(); var report = new ValidationReport("MIG");
            index = LegacyContentIndex.Load(Path.GetFullPath("Import/Legacy"), report); Assert.IsFalse(report.HasErrors, report.ToText());
            blocks = new Dictionary<string, BlockDefinition>(StringComparer.Ordinal);
            foreach (var file in index.OfType("WeaponBlockItem"))
            {
                string id = ReadWeapon(file).RegistryKey;
                var source = AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/" + id + ".asset"); Assert.NotNull(source, id);
                var clone = Object.Instantiate(source); clone.name = source.name;
                using (var so = new SerializedObject(clone))
                {
                    var modules = so.FindProperty("modules");
                    for (int i = 0; i < modules.arraySize; i++)
                        if (modules.GetArrayElementAtIndex(i).managedReferenceValue is WeaponMountModule)
                            modules.GetArrayElementAtIndex(i).FindPropertyRelative("weapon").objectReferenceValue = null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                AssetDatabase.CreateAsset(clone, Root + "/Blocks/" + id + ".asset"); blocks.Add(id, clone);
            }
            AssetDatabase.SaveAssets();
            var definitions = blocks.Values.Cast<ContentDefinition>().ToList();
            definitions.Add(AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"));
            definitions.Add(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"));
            foreach (string cueId in new[] { "cue_weapon_plasma_fire", "cue_weapon_laser_fire" })
            {
                var cue = AssetDatabase.LoadAssetAtPath<AudioCueDefinition>("Assets/Game/Content/Audio/" + cueId + ".asset"); Assert.NotNull(cue, cueId); definitions.Add(cue);
            }
            catalog = ContentCatalog.Build(definitions, report); Assert.IsFalse(report.HasErrors, report.ToText());
        }
        [TearDown] public void Clean() { if (ownsRoot) AssetDatabase.DeleteAsset(Root); ownsRoot = false; }
        LegacyMigrationContext Convert()
        {
            var report = new ValidationReport("MIG");
            var context = new LegacyMigrationContext(index, LegacyIdMap.Default, new LegacyAssetWriter(Root, manifest, false, report), report, false, Path.GetFullPath("Import/Legacy")) { Catalog = catalog };
            new WeaponConverter().Convert(context); return context;
        }
        LegacyWeaponBlockItemDto ReadWeapon(LegacyFile file)
        {
            var report = new ValidationReport("MIG");
            var dto = LegacyJson.Read<LegacyWeaponBlockItemDto>(file, report);
            Assert.IsFalse(report.HasErrors, report.ToText()); Assert.NotNull(dto); return dto;
        }
        // Test assembly has no Newtonsoft reference: change disposable source text and reload the strict index.
        void ReplaceBulletDamage(string damageType)
        {
            string temporaryRoot = Path.Combine(Path.GetTempPath(), "WeaponMigration_" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (var file in index.Files.Where(f => f.Type == "WeaponBlockItem" || f.Type == "Projectile"))
                {
                    string text = File.ReadAllText(Path.Combine("Import/Legacy", file.RelativePath));
                    if (file.Name == "projectile_bullet")
                        text = Regex.Replace(text, "(\"DamageType\"\\s*:\\s*\")[^\"]+(\")", m => m.Groups[1].Value + damageType + m.Groups[2].Value);
                    string target = Path.Combine(temporaryRoot, file.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)); File.WriteAllText(target, text);
                }
                var report = new ValidationReport("MIG"); index = LegacyContentIndex.Load(temporaryRoot, report); Assert.IsFalse(report.HasErrors, report.ToText());
            }
            finally { if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }
        }
        WeaponDefinition Weapon(string id) => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root + "/Weapons/" + id + ".asset");
        static void SetLink(BlockDefinition block, WeaponDefinition weapon)
        {
            using var so = new SerializedObject(block);
            var modules = so.FindProperty("modules");
            for (int i = 0; i < modules.arraySize; i++)
                if (modules.GetArrayElementAtIndex(i).managedReferenceValue is WeaponMountModule)
                    modules.GetArrayElementAtIndex(i).FindPropertyRelative("weapon").objectReferenceValue = weapon;
            so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(block);
        }

        [Test] public void RealExport_ValuesMatchSpec21()
        {
            var context = Convert(); Assert.IsFalse(context.Report.HasErrors, context.Report.ToText());
            Assert.AreEqual(9, AssetDatabase.FindAssets("t:WeaponDefinition", new[] { Root }).Length);
            string[] fields = NumericFields.Split('|');
            var dps = new Dictionary<string, float> { ["wpn_plasma_turret_small"] = 120, ["wpn_plasma_turret_small_fixed"] = 150, ["wpn_plasma_turret_medium"] = 315,
                ["wpn_small_beam_turret"] = 90, ["wpn_fixed_beam_turret_small"] = 108, ["wpn_railgun_medium"] = 300, ["wpn_ark_lance_medium"] = 90, ["wpn_flak_cannon_medium"] = 240, ["wpn_missile_launcher_small"] = 180 };
            foreach (string line in Rows.Split('\n'))
            {
                string[] row = line.Trim().Split('|'); Assert.AreEqual(fields.Length + 1, row.Length);
                var weapon = Weapon(row[0]); Assert.NotNull(weapon, row[0]); Assert.AreEqual(row[0], weapon.IdString);
                using var so = new SerializedObject(weapon);
                for (int i = 0; i < fields.Length; i++)
                {
                    var p = so.FindProperty(fields[i]); Assert.NotNull(p, fields[i]);
                    float actual = p.propertyType == SerializedPropertyType.Float ? p.floatValue : p.intValue;
                    Assert.AreEqual(float.Parse(row[i + 1], CultureInfo.InvariantCulture), actual, .0001f, row[0] + "/" + fields[i]);
                }
                Assert.AreEqual(row[0].Contains("flak") || row[0].Contains("missile"), weapon.ExplodeOnExpire);
                Assert.AreEqual(dps[row[0]], weapon.SustainedDps, .001f, row[0]);
                Assert.IsNull(weapon.MuzzleFx); Assert.IsNull(weapon.HitFx); Assert.IsNull(weapon.DeliveryData); Assert.AreEqual(Color.white, weapon.TracerColor);
                var validation = new ValidationReport("Weapon"); string path = AssetDatabase.GetAssetPath(weapon);
                weapon.Validate(validation.For(path), new ContentValidationContext(path, true, _ => true)); Assert.IsFalse(validation.HasErrors, validation.ToText());
            }
            foreach (var file in index.OfType("WeaponBlockItem"))
            {
                var dto = ReadWeapon(file); string key = dto.RegistryKey; Assert.IsTrue(LegacyIdMap.Default.TryMap(LegacyIdKind.Weapon, key, out string id));
                Assert.AreEqual(dto.ItemName, Weapon(id).DisplayName);
            }
            var bytes = AssetDatabase.FindAssets("t:WeaponDefinition", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath)
                .ToDictionary(p => p, p => (Bytes: File.ReadAllBytes(p), Guid: AssetDatabase.AssetPathToGUID(p)));
            var again = Convert(); Assert.IsFalse(again.Report.HasErrors, again.Report.ToText());
            Assert.AreEqual(9, again.Writer.Outcomes.Count); Assert.IsTrue(again.Writer.Outcomes.All(o => o.Outcome == WriteOutcome.Unchanged));
            foreach (var pair in bytes) { CollectionAssert.AreEqual(pair.Value.Bytes, File.ReadAllBytes(pair.Key)); Assert.AreEqual(pair.Value.Guid, AssetDatabase.AssetPathToGUID(pair.Key)); }
        }

        [Test] public void DamageTypeMapped()
        {
            var context = Convert(); Assert.IsFalse(context.Report.HasErrors, context.Report.ToText());
            foreach (var file in index.OfType("WeaponBlockItem"))
            {
                var dto = ReadWeapon(file); string key = dto.RegistryKey; LegacyIdMap.Default.TryMap(LegacyIdKind.Weapon, key, out string id);
                Assert.IsTrue(index.TryResolve(dto.ProjectileData, out var projectileFile));
                var projectile = LegacyJson.Read<LegacyProjectileDto>(projectileFile, new ValidationReport("MIG")); Assert.NotNull(projectile);
                string type = projectile.DamageType, visual = projectile.VisualType;
                DamageType expected = type switch { "Kinetic" => DamageType.Kinetic, "Energy" => DamageType.Energy, "HighExplosive" => DamageType.Explosive, _ => throw new AssertionException(type) };
                if (id == "wpn_missile_launcher_small") expected = DamageType.Explosive; // Explicit v2 retune, SPEC-72 Q20.
                WeaponDelivery delivery = projectile.Speed == 0 && visual == "LaserBeam" ? WeaponDelivery.Beam :
                    projectile.IsHoming ? WeaponDelivery.Missile : WeaponDelivery.Projectile;
                Assert.AreEqual(expected, Weapon(id).DamageType, id); Assert.AreEqual(delivery, Weapon(id).Delivery, id);
                Assert.AreEqual(visual, Weapon(id).Visual.ToString(), id);
                LegacyIdMap.Default.TryMap(LegacyIdKind.AudioCue, dto.MuzzleSoundID, out string cueId);
                Assert.AreEqual(cueId, Weapon(id).FireCue == null ? null : Weapon(id).FireCue.IdString, id);
            }
            ReplaceBulletDamage("HighExplosive");
            var mapped = Convert(); Assert.IsFalse(mapped.Report.HasErrors, mapped.Report.ToText()); Assert.AreEqual(DamageType.Explosive, Weapon("wpn_plasma_turret_small").DamageType);
            ReplaceBulletDamage("UnknownDamage");
            var invalid = Convert(); Assert.AreEqual(3, invalid.Report.Entries.Count(e => e.Code == "MIG-E021"));
            Assert.AreEqual(6, invalid.Writer.Outcomes.Count); Assert.IsTrue(invalid.Writer.Outcomes.All(o => o.Outcome == WriteOutcome.Unchanged));
            Assert.AreEqual(DamageType.Explosive, Weapon("wpn_plasma_turret_small").DamageType, "An unknown identity must not overwrite a valid asset");
        }

        [Test] public void WeaponBlocks_AreLinked()
        {
            var preexisting = new Dictionary<string, (WeaponDefinition Weapon, string Guid)>();
            foreach (string suffix in new[] { "plasma_turret_small", "plasma_turret_small_fixed", "plasma_turret_medium" })
            {
                string id = "wpn_" + suffix; var source = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Game/Content/Weapons/" + id + ".asset"); Assert.NotNull(source);
                var clone = Object.Instantiate(source); clone.name = source.name;
                using (var so = new SerializedObject(clone)) { so.FindProperty("fireCue").objectReferenceValue = null; so.FindProperty("damage").floatValue = 99; so.ApplyModifiedPropertiesWithoutUndo(); }
                string path = Root + "/Weapons/" + id + ".asset"; AssetDatabase.CreateAsset(clone, path); SetLink(blocks["weapon_" + suffix], clone);
                preexisting.Add(id, (clone, AssetDatabase.AssetPathToGUID(path)));
            }
            // The link must use the mount's actual array index, while retaining its SerializeReference identity.
            var railgun = blocks["weapon_railgun_medium"]; var mount = railgun.Modules.Single();
            using (var so = new SerializedObject(railgun))
            {
                var modules = so.FindProperty("modules"); modules.arraySize = 2; modules.GetArrayElementAtIndex(1).managedReferenceValue = mount;
                var storage = new PowerStorageModule { Params = new PowerStorageParams { Storage = 40 } };
                Assert.IsTrue(UnityEngine.Serialization.ManagedReferenceUtility.SetManagedReferenceIdForObject(railgun, storage, (long)storage.Id));
                modules.GetArrayElementAtIndex(0).managedReferenceValue = storage; so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(railgun);
            }
            var context = Convert(); Assert.IsFalse(context.Report.HasErrors, context.Report.ToText());
            Assert.AreEqual(6, context.Writer.Outcomes.Count(o => o.Outcome == WriteOutcome.Created));
            Assert.AreEqual(3, context.Writer.Outcomes.Count(o => o.Outcome == WriteOutcome.SkippedHandEdited));
            var registered = blocks.Values.Cast<ContentDefinition>().Concat(Rows.Split('\n').Select(row => Weapon(row.Split('|')[0])))
                .Concat(catalog.All<AudioCueDefinition>()).Concat(new ContentDefinition[] { catalog.MissingBlock, catalog.MissingItem }).ToArray();
            var registrationReport = new ValidationReport("Catalog");
            var validationCatalog = ContentCatalog.Build(registered, registrationReport);
            Assert.IsFalse(registrationReport.HasErrors, registrationReport.ToText());
            foreach (var pair in blocks)
            {
                Assert.IsTrue(LegacyIdMap.Default.TryMap(LegacyIdKind.Weapon, pair.Key, out string id));
                Assert.IsTrue(pair.Value.TryGetModule<WeaponMountModule>(out var linked)); Assert.AreSame(Weapon(id), linked.Weapon, id);
                var validation = new ValidationReport("Block"); string path = AssetDatabase.GetAssetPath(pair.Value);
                pair.Value.Validate(validation.For(path), new ContentValidationContext(path, true, d => registered.Contains(d), validationCatalog));
                Assert.IsFalse(validation.Entries.Any(e => e.Code == "DATA-E047"), validation.ToText());
                Assert.IsFalse(manifest.TryGetHash(path, out _), "Filling a hand-edited block's link must not adopt its tuning");
            }
            foreach (var pair in preexisting)
            {
                Assert.AreSame(pair.Value.Weapon, Weapon(pair.Key)); Assert.AreEqual(99, pair.Value.Weapon.Damage);
                Assert.AreEqual(pair.Value.Guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pair.Value.Weapon)));
                Assert.NotNull(pair.Value.Weapon.FireCue, "Missing sound links may be filled without replacing the weapon");
                Assert.IsFalse(manifest.TryGetHash(AssetDatabase.GetAssetPath(pair.Value.Weapon), out _));
            }
            SetLink(railgun, preexisting["wpn_plasma_turret_small"].Weapon);
            var again = Convert(); Assert.IsFalse(again.Report.HasErrors, again.Report.ToText());
            railgun.TryGetModule<WeaponMountModule>(out var kept); Assert.AreSame(preexisting["wpn_plasma_turret_small"].Weapon, kept.Weapon, "A nonempty hand-edited link is preserved");
            Assert.IsTrue(again.Report.Entries.Any(e => e.Code == "MIG-I012"));
        }
    }
}
