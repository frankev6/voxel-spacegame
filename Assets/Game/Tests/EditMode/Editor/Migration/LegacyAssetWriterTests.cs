using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Editor.Migration
{
    public sealed class LegacyAssetWriterTests
    {
        const string Root = "Assets/Game/Content/_MigrationTest";
        const string PathA = Root + "/Items/item_test.asset";
        string temporary;
        MigrationManifest manifest;
        ValidationReport report;
        LegacyAssetWriter writer;
        [SetUp] public void SetUp()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root), "Do not delete existing user content.");
            temporary = Path.Combine(Path.GetTempPath(), "migration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            manifest = MigrationManifest.Load(Path.Combine(temporary, "manifest.json")); report = new ValidationReport("MIG");
            writer = new LegacyAssetWriter(Root, manifest, false, report);
        }
        [TearDown] public void TearDown()
        {
            AssetDatabase.DeleteAsset(Root);
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
        ItemDefinition Write(out WriteOutcome outcome, LegacyAssetWriter use = null) => (use ?? writer).CreateOrUpdate<ItemDefinition>("Items", "item_test", so =>
        {
            so.FindProperty("displayName").stringValue = "Test";
            so.FindProperty("basePrice").intValue = 17;
        }, out outcome);
        static void Edit(ItemDefinition asset, int price, bool save = true)
        {
            using var so = new SerializedObject(asset); so.FindProperty("basePrice").intValue = price; so.ApplyModifiedPropertiesWithoutUndo();
            if (save) AssetDatabase.SaveAssetIfDirty(asset);
        }
        [Test] public void Create_ThenUnchanged()
        {
            var a = Write(out var first); Assert.AreEqual(WriteOutcome.Created, first);
            string guid = AssetDatabase.AssetPathToGUID(PathA); byte[] before = File.ReadAllBytes(PathA);
            Assert.AreEqual("item_test", a.IdString);
            Assert.AreSame(a, Write(out var second)); Assert.AreEqual(WriteOutcome.Unchanged, second);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(PathA)); Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(PathA));
        }
        [Test] public void HandEdited_IsKept_ForceOverwrites()
        {
            var a = Write(out _); string guid = AssetDatabase.AssetPathToGUID(PathA); Edit(a, 99);
            Write(out var kept); Assert.AreEqual(WriteOutcome.SkippedHandEdited, kept); Assert.AreEqual(99, a.BasePrice);
            Assert.IsTrue(report.Entries.Any(e => e.Code == "MIG-I011"));
            Write(out var forced, new LegacyAssetWriter(Root, manifest, true, report));
            Assert.AreEqual(WriteOutcome.Updated, forced); Assert.AreEqual(17, a.BasePrice); Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(PathA));
            Edit(a, 101, false); Write(out kept); Assert.AreEqual(WriteOutcome.SkippedHandEdited, kept); Assert.AreEqual(101, a.BasePrice);
        }
        [Test] public void SetLinkIfEmpty_Rules()
        {
            var a = Write(out _); Edit(a, 99);
            var texture = new Texture2D(2, 2); AssetDatabase.CreateAsset(texture, Root + "/texture.asset");
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero); AssetDatabase.AddObjectToAsset(sprite, texture);
            var other = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero); AssetDatabase.AddObjectToAsset(other, texture); AssetDatabase.SaveAssets();
            Assert.IsTrue(writer.SetLinkIfEmpty(a, "icon", sprite)); Assert.AreEqual(sprite, a.Icon);
            Assert.IsFalse(writer.SetLinkIfEmpty(a, "icon", other)); Assert.AreEqual(sprite, a.Icon);
            Assert.IsFalse(writer.SetLinkIfEmpty(a, "icon", sprite)); Assert.IsTrue(report.Entries.Any(e => e.Code == "MIG-I012"));
            Write(out var kept); Assert.AreEqual(WriteOutcome.SkippedHandEdited, kept); Assert.AreEqual(99, a.BasePrice);
            // Reload the manifest, as the next migration run does: link filling must not adopt the hand edit.
            string path = Path.Combine(temporary, "manifest.json"); manifest.Save(path);
            Write(out kept, new LegacyAssetWriter(Root, MigrationManifest.Load(path), false, report));
            Assert.AreEqual(WriteOutcome.SkippedHandEdited, kept); Assert.AreEqual(99, a.BasePrice);
        }
        [Test] public void LinkArray_OnlyFillsEmptyAndPreservesOwnership()
        {
            var config = writer.CreateOrUpdate<AudioConfigDefinition>("Audio", "audio_config", so => so.FindProperty("uiSounds").arraySize = 0, out _);
            var cue = writer.CreateOrUpdate<AudioCueDefinition>("Audio", "cue_test", null, out _);
            Assert.IsTrue(writer.SetLinkArrayIfEmpty(config, "uiSounds", new Object[] { cue }));
            Assert.IsFalse(writer.SetLinkArrayIfEmpty(config, "uiSounds", new Object[] { null }));
            using var so = new SerializedObject(config);
            Assert.AreEqual(cue, so.FindProperty("uiSounds").GetArrayElementAtIndex(0).objectReferenceValue);
            string path = AssetDatabase.GetAssetPath(config);
            Assert.IsTrue(manifest.TryGetHash(path, out ulong hash)); Assert.AreEqual(StableHash.Hash64(File.ReadAllBytes(path)), hash);
        }
        [Test] public void MissingProperty_IsW090()
        {
            var a = writer.CreateOrUpdate<ItemDefinition>("Items", "item_test", so =>
            {
                writer.Set(so, "notAField", p => p.intValue = 1);
                writer.Set(so, "displayName", p => p.stringValue = "Still written");
            }, out _);
            Assert.AreEqual("Still written", a.DisplayName);
            Assert.AreEqual(1, report.Entries.Count(e => e.Code == "MIG-W090"));
            Assert.IsFalse(writer.SetLinkIfEmpty(a, "notALink", null));
            Assert.AreEqual(2, report.Entries.Count(e => e.Code == "MIG-W090"));
        }
        [Test] public void ReportAndManifest_AreDeterministic()
        {
            Write(out _); string m = Path.Combine(temporary, "manifest.json"), r = Path.Combine(temporary, "report.md");
            byte[] manifestBytes = null, reportBytes = null;
            for (int i = 0; i < 2; i++)
            {
                var next = new LegacyAssetWriter(Root, manifest, false, report); Write(out var result, next); Assert.AreEqual(WriteOutcome.Unchanged, result);
                manifest.SetHash("Assets/z", 1); manifest.SetHash("Assets/a", 2); manifest.Save(m);
                MigrationReportWriter.Write(r, Array.Empty<ILegacyConverter>(), next, report, Array.Empty<MigrationShipRow>());
                if (i == 0) { manifestBytes = File.ReadAllBytes(m); reportBytes = File.ReadAllBytes(r); }
                else { CollectionAssert.AreEqual(manifestBytes, File.ReadAllBytes(m)); CollectionAssert.AreEqual(reportBytes, File.ReadAllBytes(r)); }
                manifest = MigrationManifest.Load(m);
            }
            string json = File.ReadAllText(m); Assert.IsFalse(json.Contains('\r')); Assert.Less(json.IndexOf("Assets/a", StringComparison.Ordinal), json.IndexOf("Assets/z", StringComparison.Ordinal));
            Assert.IsTrue(json.Contains("0000000000000002")); Assert.IsFalse(File.ReadAllText(r).Contains('\r'));
        }
        [Test] public void UnknownOwnershipWrongTypeDeletionAndEscapes()
        {
            var a = Write(out _);
            Write(out var kept, new LegacyAssetWriter(Root, new MigrationManifest(), false, report)); Assert.AreEqual(WriteOutcome.SkippedHandEdited, kept);
            Assert.IsNull(writer.CreateOrUpdate<BlockDefinition>("Items", "item_test", null, out var wrong)); Assert.AreEqual(WriteOutcome.Failed, wrong);
            Assert.IsTrue(report.Entries.Any(e => e.Code == "MIG-E010"));
            Assert.Throws<ArgumentException>(() => writer.CreateOrUpdate<ItemDefinition>("../Outside", "item_test", null, out _));
            Assert.Throws<ArgumentException>(() => writer.CreateOrUpdate<ItemDefinition>("Items", "../../item_bad", null, out _));
            var outside = ScriptableObject.CreateInstance<ItemDefinition>();
            try { Assert.Throws<ArgumentException>(() => writer.SetLinkIfEmpty(outside, "icon", null)); }
            finally { Object.DestroyImmediate(outside); }
            AssetDatabase.DeleteAsset(PathA); Write(out var recreated); Assert.AreEqual(WriteOutcome.Created, recreated);
        }
        [Test] public void ThrowingFill_PreservesExistingBytesAndHash()
        {
            Write(out _); byte[] before = File.ReadAllBytes(PathA); Assert.IsTrue(manifest.TryGetHash(PathA, out ulong hash));
            Assert.Throws<InvalidOperationException>(() => writer.CreateOrUpdate<ItemDefinition>("Items", "item_test", so =>
            { so.FindProperty("basePrice").intValue = 900; throw new InvalidOperationException("fixture"); }, out _));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(PathA)); Assert.AreEqual(hash, StableHash.Hash64(File.ReadAllBytes(PathA)));
        }
        [Test] public void MalformedManifest_IsRejected()
        {
            string p = Path.Combine(temporary, "bad.json");
            foreach (string json in new[] { "[]", "{\"x\":\"bad\"}", "{\"x\":1}", "{\"x\":\"0000000000000000\",\"x\":\"0000000000000001\"}" })
            { File.WriteAllText(p, json); Assert.Catch(() => MigrationManifest.Load(p)); }
        }
    }
}
