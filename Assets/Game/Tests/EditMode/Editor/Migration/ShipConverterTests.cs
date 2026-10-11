using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Migration;
using Game.Editor.Migration.Converters;
using Game.Editor.Migration.Dto;
using Game.Tests.EditMode.Data;
using Game.Voxel.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Migration
{
    public sealed class ShipConverterTests
    {
        const string Root = "Assets/Game/Content/_MigrationTest";
        LegacyMigrationContext context;
        bool ownsRoot;
        internal const string Rows = @"ship_base,4,0,4,0,4,1,1,1
ship_bastion,343,0,334,9,361,3,3,0
ship_beetle,66,0,64,2,91,2,6,1
ship_butterfly,757,0,757,0,815,14,21,1
ship_crescent,270,2,259,9,306,11,24,1
ship_fighter,965,0,965,0,1005,46,29,1
ship_freighter,343,0,334,9,361,3,3,0
ship_frigate,119,0,119,0,127,19,9,1
ship_icarus,270,2,258,10,305,3,23,0
ship_red_elite,789,0,787,2,819,2,22,0
ship_shuttle,119,0,119,0,127,19,9,1
ship_striker,50,0,44,6,74,2,14,0
ship_vestari,946,0,946,0,972,36,21,1";
        [OneTimeSetUp] public void ConvertExport()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root)); ownsRoot = true;
            var report = new ValidationReport("MIG"); string legacyRoot = Path.GetFullPath("Import/Legacy");
            var defs = AssetDatabase.FindAssets("t:BlockDefinition", new[] { "Assets/Game/Content/Blocks" }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BlockDefinition>).Cast<ContentDefinition>().ToList();
            defs.Add(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"));
            context = new LegacyMigrationContext(LegacyContentIndex.Load(legacyRoot, report), LegacyIdMap.Default,
                new LegacyAssetWriter(Root, new MigrationManifest(), false, report), report, false, legacyRoot) { Catalog = ContentCatalog.Build(defs, report) };
            new ShipConverter().Convert(context); Assert.IsFalse(report.HasErrors, report.ToText());
        }
        [OneTimeTearDown] public void Clean() { if (ownsRoot) AssetDatabase.DeleteAsset(Root); }
        [Test] public void RealExport_MatchesExpectedTable()
        {
            Assert.AreEqual(13, context.Ships.Count);
            foreach (string line in Rows.Split('\n'))
            {
                var e = line.Trim().Split(','); var r = context.Ships.Single(s => s.Ship == e[0]);
                CollectionAssert.AreEqual(e.Skip(1).Select(int.Parse), new[] { r.LegacyVoxels, r.EmptyKeys, r.Placed, r.DroppedOverlaps, r.Cells, r.Palette, r.Instances, r.Cores }, e[0]);
                var blueprint = AssetDatabase.LoadAssetAtPath<ShipBlueprintDefinition>(Root + "/Ships/" + e[0] + ".asset");
                Assert.NotNull(blueprint); Assert.AreEqual(r.Cells, blueprint.CellCount); Assert.AreEqual(.5f, blueprint.CellSize); Assert.AreEqual(VoxsCodec.Version, blueprint.PayloadVersion);
                var validation = new ValidationReport("Blueprint"); blueprint.Validate(validation.For(e[0]), null); Assert.IsFalse(validation.HasErrors, validation.ToText());
            }
        }
        [Test] public void Voxs_RoundTrips()
        {
            foreach (var file in context.Index.OfType("ShipItem"))
            {
                var dto = LegacyJson.Read<LegacyShipItemDto>(file, context.Report);
                using var built = LegacyShipBuilder.Build(dto, key => ShipConverter.Resolve(context, key), new ValidationReport("MIG"), file.RelativePath);
                var blueprint = AssetDatabase.LoadAssetAtPath<ShipBlueprintDefinition>(Root + "/Ships/" + dto.RegistryKey + ".asset");
                Assert.AreEqual(dto.ItemName, blueprint.DisplayName);
                Assert.IsTrue(VoxsCodec.TryDecode(blueprint.Payload, context.Catalog, Allocator.TempJob, out var decoded, out string error), error);
                using (decoded)
                {
                    Assert.IsTrue(ShipConverter.Matches(built.Source, decoded), dto.RegistryKey);
                    var occupied = new HashSet<int3>(); var owned = new Dictionary<int, HashSet<int3>>();
                    for (int b = 0; b < decoded.BrickCoords.Length; b++) for (int local = 0; local < 4096; local++)
                    {
                        Cell cell = decoded.Cells[b * 4096 + local]; if (cell.IsAir) continue;
                        int3 position = (decoded.BrickCoords[b] << 4) + new int3(local & 15, (local >> 4) & 15, local >> 8);
                        Assert.IsTrue(occupied.Add(position));
                        if (cell.IsInstance) { if (!owned.TryGetValue(cell.InstanceIndex, out var cells)) owned.Add(cell.InstanceIndex, cells = new HashSet<int3>()); Assert.IsTrue(cells.Add(position)); }
                    }
                    for (int i = 0; i < decoded.Instances.Length; i++)
                    {
                        var instance = decoded.Instances[i]; var def = context.Catalog.GetBlock(instance.Block);
                        var expected = def.Cells.Select(c => instance.Origin + OrientationTables.Rotate(new int3(c.x, c.y, c.z), instance.Orient));
                        CollectionAssert.AreEquivalent(expected, owned[i], dto.RegistryKey + " instance " + i);
                    }
                    Assert.AreEqual(blueprint.CellCount, occupied.Count);
                    Assert.AreEqual(blueprint.BoundsMin, new UnityEngine.Vector3Int(built.BoundsMin.x, built.BoundsMin.y, built.BoundsMin.z));
                    Assert.AreEqual(blueprint.BoundsMax, new UnityEngine.Vector3Int(built.BoundsMax.x, built.BoundsMax.y, built.BoundsMax.z));
                    byte[] encoded = VoxsCodec.Encode(built.Source, VoxsBlockNames.FromCatalog(context.Catalog), VoxsEncodeOptions.ForAsset);
                    CollectionAssert.AreEqual(blueprint.Payload.ToArray(), encoded);
                }
            }
        }
        [Test] public void SecondRun_Unchanged()
        {
            var paths = context.Writer.Outcomes.Select(o => o.AssetPath).Distinct().ToArray();
            var bytes = paths.ToDictionary(p => p, File.ReadAllBytes); var metas = paths.ToDictionary(p => p, p => File.ReadAllBytes(p + ".meta"));
            int start = context.Writer.Outcomes.Count; context.Ships.Clear(); new ShipConverter().Convert(context);
            var second = context.Writer.Outcomes.Skip(start).ToArray(); Assert.AreEqual(13, second.Length);
            Assert.IsTrue(second.All(o => o.Outcome == WriteOutcome.Unchanged));
            foreach (string path in paths) { CollectionAssert.AreEqual(bytes[path], File.ReadAllBytes(path)); CollectionAssert.AreEqual(metas[path], File.ReadAllBytes(path + ".meta")); }
        }
        [Test] public void Blueprint_ValidationForMigration()
        {
            try
            {
                var b = TestContentFactory.Create<ShipBlueprintDefinition>("ship_validation");
                Assert.AreEqual("VOX-E001", TestContentFactory.Validate(b).Entries.Single().Code);
                TestContentFactory.Edit(b, so => { so.FindProperty("payload").arraySize = 1; });
                Assert.IsFalse(TestContentFactory.Validate(b).HasErrors);
                TestContentFactory.Edit(b, so => { so.FindProperty("cellSize").floatValue = 0; });
                Assert.AreEqual("VOX-E002", TestContentFactory.Validate(b).Entries.Single().Code);
                TestContentFactory.Edit(b, so => { so.FindProperty("cellSize").floatValue = .5f; so.FindProperty("payloadVersion").intValue = 2; });
                Assert.AreEqual("VOX-E003", TestContentFactory.Validate(b).Entries.Single().Code);
                TestContentFactory.Edit(b, so => { so.FindProperty("payloadVersion").intValue = VoxsCodec.Version; });
                Assert.IsFalse(TestContentFactory.Validate(b).HasErrors);
            }
            finally { TestContentFactory.DestroyAll(); }
        }
    }
}
