using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Data.Validation;
using Game.Editor.Migration;
using Game.Editor.Migration.Dto;
using NUnit.Framework;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>MIG-002: indexing and strict reading of the real legacy export (SPEC-72 §4.2, §4.3, decision 3).</summary>
    public sealed class LegacyJsonTests
    {
        static string LegacyRoot => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Import", "Legacy"));

        static readonly Dictionary<string, Type> DtoByLegacyType = new Dictionary<string, Type>
        {
            { "ResourceItem", typeof(LegacyItemDto) }, { "SlottableItem", typeof(LegacySlottableItemDto) }, { "PaintBucketItem", typeof(LegacyPaintBucketItemDto) },
            { "BlockItem", typeof(LegacyBlockItemDto) }, { "CoreBlockItem", typeof(LegacyCoreBlockItemDto) }, { "ShieldBlockItem", typeof(LegacyShieldBlockItemDto) },
            { "PortalBlockItem", typeof(LegacyPortalBlockItemDto) }, { "WeaponBlockItem", typeof(LegacyWeaponBlockItemDto) }, { "MirrorBlockItem", typeof(LegacyMirrorBlockItemDto) },
            { "ShipItem", typeof(LegacyShipItemDto) }, { "Projectile", typeof(LegacyProjectileDto) }, { "FactionData", typeof(LegacyFactionDto) },
            { "ShieldToggleAbilitySO", typeof(LegacyShieldToggleAbilityDto) }, { "BoostAbilitySO", typeof(LegacyBoostAbilityDto) }, { "AudioLibrary", typeof(LegacyAudioLibraryDto) },
            { "LootTable", typeof(LegacyLootTableDto) }, { "ShipTableSO", typeof(LegacyShipTableDto) }, { "SolarSystemEntity", typeof(LegacySystemEntityDto) },
            { "PlanetEntity", typeof(LegacyPlanetEntityDto) }, { "StarEntity", typeof(LegacyStarEntityDto) },
            { "Assets.Game.Core.Galaxy_Map.HyperlaneEntity", typeof(LegacyHyperlaneEntityDto) }, { "NPCFleetEntity", typeof(LegacyNpcFleetEntityDto) },
            { "CombatEncounter", typeof(LegacyCombatEncounterDto) }, { "SolarSystemData", typeof(LegacySolarSystemDataDto) },
            { "GalaxyData", typeof(LegacyGalaxyDataDto) }, { "OpenWorldGalaxyConfig", typeof(LegacyOpenWorldGalaxyConfigDto) },
        };

        [Test]
        public void RealExport_IndexesAllFiles()
        {
            var report = new ValidationReport("MIG");
            var index = LegacyContentIndex.Load(LegacyRoot, report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Assert.AreEqual(155, index.Files.Count);
            CollectionAssert.IsOrdered(index.Files.Select(f => f.RelativePath).ToArray(), StringComparer.Ordinal);
            Assert.IsTrue(index.Files.All(f => f.RelativePath.StartsWith("content/", StringComparison.Ordinal)));
            Assert.AreEqual(13, index.OfType("ShipItem").Count);
            Assert.AreEqual(0, index.OfType("NoSuchType").Count);
            var hull = index.Files.First(f => f.RelativePath.EndsWith("/block_hull.json", StringComparison.Ordinal));
            Assert.IsTrue(index.TryGetByAssetPath(hull.AssetPath, out var same)); Assert.AreSame(hull, same);
            Assert.IsFalse(index.TryResolve(default, out _), "a null reference resolves to nothing");
        }

        [Test]
        public void RealExport_AllTypesReadStrictly()
        {
            var report = new ValidationReport("MIG");
            var index = LegacyContentIndex.Load(LegacyRoot, report);
            var read = typeof(LegacyJson).GetMethod(nameof(LegacyJson.Read));
            int count = 0;
            foreach (var kv in DtoByLegacyType)
            {
                var files = index.OfType(kv.Key);
                Assert.Greater(files.Count, 0, $"no {kv.Key} files in the export");
                foreach (var file in files)
                {
                    var dto = (LegacyDto)read.MakeGenericMethod(kv.Value).Invoke(null, new object[] { file, report });
                    Assert.IsNotNull(dto, $"{file.RelativePath}: {report.ToText()}");
                    Assert.AreEqual(kv.Key, dto.Type); Assert.AreEqual(file.AssetPath, dto.Path);
                    count++;
                }
            }
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Assert.Greater(count, 80);

            var railgun = LegacyJson.Read<LegacyWeaponBlockItemDto>(index.Files.First(f => f.RelativePath.EndsWith("/weapon_railgun_medium.json", StringComparison.Ordinal)), report);
            Assert.AreEqual("weapon_railgun_medium", railgun.RegistryKey);
            Assert.IsFalse(railgun.ProjectileData.IsNull, "references are read");
            Assert.IsTrue(index.TryResolve(railgun.ProjectileData, out var projectile)); Assert.AreEqual("Projectile", projectile.Type);
        }

        [Test]
        public void ExtraProperty_IsE002()
        {
            string root = Path.Combine(Path.GetTempPath(), "LegacyJsonTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "content"));
            try
            {
                File.WriteAllText(Path.Combine(root, "content", "extra.json"),
                    "{\"$path\":\"Assets/Resources/x.asset\",\"$type\":\"ShipTableSO\",\"$name\":\"x\",\"ships\":[],\"Surprise\":1}");
                File.WriteAllText(Path.Combine(root, "content", "badref.json"),
                    "{\"$path\":\"Assets/Resources/y.asset\",\"$type\":\"ShipTableSO\",\"$name\":\"y\",\"ships\":[{\"$ref\":\"a\",\"name\":\"a\",\"type\":\"t\",\"odd\":2}]}");
                File.WriteAllText(Path.Combine(root, "content", "nopath.json"), "{\"$type\":\"ShipTableSO\"}");
                var report = new ValidationReport("MIG");
                var index = LegacyContentIndex.Load(root, report);
                Assert.AreEqual(2, index.Files.Count);
                Assert.AreEqual(1, report.ErrorCount, "nopath.json has no $path");
                Assert.AreEqual("content/nopath.json", report.Entries[0].AssetPath);

                report = new ValidationReport("MIG");
                Assert.IsNull(LegacyJson.Read<LegacyShipTableDto>(index.Files.Single(f => f.Name == "x"), report));
                Assert.AreEqual("MIG-E002", report.Entries.Single().Code);
                Assert.AreEqual("content/extra.json", report.Entries[0].AssetPath);
                Assert.AreEqual("Surprise", report.Entries[0].Field, report.ToText());

                report = new ValidationReport("MIG");
                Assert.IsNull(LegacyJson.Read<LegacyShipTableDto>(index.Files.Single(f => f.Name == "y"), report));
                StringAssert.Contains("ships[0]", report.Entries.Single().Field, "unknown property inside a reference");

                report = new ValidationReport("MIG");
                Assert.IsNull(LegacyJson.ReadFile<LegacyShipTableDto>(root, "content/extra.json", report));
                Assert.AreEqual("Surprise", report.Entries.Single().Field);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void ContentRefs_AllResolve()
        {
            var report = new ValidationReport("MIG");
            var index = LegacyContentIndex.Load(LegacyRoot, report);
            var assetExtensions = new[] { ".png", ".obj", ".wav", ".ogg", ".prefab", ".mat", ".fbx" };
            var unresolved = new List<string>();
            int refs = 0;
            foreach (var file in index.Files)
            {
                string text = File.ReadAllText(Path.Combine(LegacyRoot, file.RelativePath));
                foreach (Match m in Regex.Matches(text, "\"\\$ref\"\\s*:\\s*\"([^\"]+)\""))
                {
                    string path = m.Groups[1].Value;
                    if (!path.StartsWith("Assets/Resources/", StringComparison.Ordinal)) continue;
                    if (assetExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase))) continue;
                    refs++;
                    if (!index.TryResolve(new LegacyRef(path, null, null, null), out _)) unresolved.Add($"{file.RelativePath} → {path}");
                }
            }
            CollectionAssert.IsEmpty(unresolved);
            Assert.Greater(refs, 50);
        }
    }
}
