using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools.Constraints;
using static Game.Tests.EditMode.Data.TestContentFactory;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-005: ContentCatalog (SPEC-04 §4.9, §5.4).</summary>
    public sealed class ContentCatalogTests
    {
        [TearDown] public void TearDown() { DestroyAll(); ContentCatalog.HashOverrideForTests = null; }

        static List<ContentDefinition> Placeholders() => new List<ContentDefinition>
        { Block("block_missing", BlockKind.Structural), Item("item_missing") };

        static string[] Codes(ValidationReport r) => r.Entries.Where(e => e.Severity == ValidationSeverity.Error).Select(e => e.Code).ToArray();

        [Test]
        public void BlockIndices_AreOrdinalAndOneBased()
        {
            var defs = Placeholders();
            defs.Add(Block("block_zeta", BlockKind.Structural)); defs.Add(Block("weapon_alpha", BlockKind.Structural)); defs.Add(Block("block_alpha", BlockKind.Structural));
            var report = new ValidationReport("test");
            var c = ContentCatalog.Build(defs, report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            CollectionAssert.AreEqual(new[] { "block_alpha", "block_missing", "block_zeta", "weapon_alpha" }, c.All<BlockDefinition>().Select(b => b.IdString).ToArray());
            Assert.AreEqual(4, c.BlockCount);
            for (ushort i = 1; i <= 4; i++) { var b = c.GetBlock(i); Assert.AreEqual(i, c.GetBlockIndex(b)); Assert.AreEqual(i, c.IndexOf(b)); }
            Assert.IsNull(c.GetBlock(0), "0 = air"); Assert.IsNull(c.GetBlock(5));
            Assert.IsTrue(c.TryGetBlockIndex(new ContentId("block_zeta"), out var z)); Assert.AreEqual(3, z);
            Assert.IsFalse(c.TryGetBlockIndex(new ContentId("item_missing"), out var none)); Assert.AreEqual(0, none);
            defs.Reverse();
            var c2 = ContentCatalog.Build(defs, new ValidationReport("test"));
            CollectionAssert.AreEqual(c.All<BlockDefinition>(), c2.All<BlockDefinition>(), "independent of input order");
        }

        [Test]
        public void Families_IncludeSubclasses()
        {
            var defs = Placeholders();
            defs.Add(Block("block_hull", BlockKind.Structural)); defs.Add(Item("item_fuel"));
            var c = ContentCatalog.Build(defs, new ValidationReport("test"));
            CollectionAssert.AreEqual(new[] { "block_hull", "block_missing", "item_fuel", "item_missing" }, c.All<ItemDefinition>().Select(d => d.IdString).ToArray());
            CollectionAssert.AreEqual(new[] { "block_hull", "block_missing" }, c.All<BlockDefinition>().Select(d => d.IdString).ToArray());
            Assert.AreEqual(4, c.Count);
            var hull = c.Get<BlockDefinition>(new ContentId("block_hull"));
            Assert.AreEqual(1, c.IndexOf<BlockDefinition>(hull)); Assert.AreEqual(1, c.IndexOf<ItemDefinition>(hull), "separate families, separate indices");
            Assert.AreEqual(0, c.IndexOf<BlockDefinition>(null));
            Assert.AreEqual(0, c.All<ShipBlueprintDefinition>().Count, "an empty family");
            Assert.Throws<KeyNotFoundException>(() => c.Get<BlockDefinition>(new ContentId("item_fuel")), "wrong type");
            Assert.IsTrue(c.IsPlaceholder(c.MissingBlock)); Assert.IsTrue(c.IsPlaceholder(c.MissingItem)); Assert.IsFalse(c.IsPlaceholder(hull));
        }

        [Test]
        public void Build_ReportsDuplicatesNullsAndMissingPlaceholders()
        {
            var r = new ValidationReport("test");
            ContentCatalog.Build(new List<ContentDefinition> { Item("item_a"), null, Item("item_a"), Item("bad-id"), Block("item_wrong", BlockKind.Structural) }, r);
            var codes = Codes(r);
            CollectionAssert.Contains(codes, "DATA-E007"); CollectionAssert.Contains(codes, "DATA-E004");
            CollectionAssert.Contains(codes, "DATA-E002"); CollectionAssert.Contains(codes, "DATA-E003");
            Assert.AreEqual(2, codes.Count(c => c == "DATA-E008"), "block_missing and item_missing");
            Assert.AreEqual("definitions[1]", r.Entries.First(e => e.Code == "DATA-E007").Field);

            r = new ValidationReport("test");
            var defs = Placeholders();
            defs.Add(Item("item_live"));
            defs.Add(Item("item_new", so => { var p = so.FindProperty("formerIds"); p.arraySize = 3; p.GetArrayElementAtIndex(0).stringValue = "item_live"; p.GetArrayElementAtIndex(1).stringValue = "Bad"; p.GetArrayElementAtIndex(2).stringValue = "item_old"; }));
            defs.Add(Item("item_other", so => { var p = so.FindProperty("formerIds"); p.arraySize = 1; p.GetArrayElementAtIndex(0).stringValue = "item_old"; }));
            ContentCatalog.Build(defs, r);
            Assert.AreEqual(3, Codes(r).Count(c => c == "DATA-E006"), "former ID equal to a live ID, invalid, and shared: " + r.ToText());
            Assert.AreEqual(3, r.ErrorCount);
        }

        [Test]
        public void Resolve_UnknownId_ReturnsMissingAndRecordsOnce()
        {
            var defs = Placeholders(); defs.Add(Item("item_fuel"));
            var c = ContentCatalog.Build(defs, new ValidationReport("test"));
            Assert.AreSame(c.MissingItem, c.ResolveItemOrMissing(new ContentId("item_gone"), "save p001"));
            Assert.AreSame(c.MissingItem, c.ResolveItemOrMissing(new ContentId("item_gone"), "save p001"));
            Assert.AreEqual(1, c.Unresolved.Count);
            Assert.AreEqual("item_gone", c.Unresolved[0].Id); Assert.AreEqual("ItemDefinition", c.Unresolved[0].ExpectedType); Assert.AreEqual("save p001", c.Unresolved[0].Context);
            Assert.AreSame(c.MissingBlock, c.ResolveBlockOrMissing(new ContentId("item_fuel"), "ship"), "a definition of the wrong type is unresolved");
            Assert.AreEqual(2, c.Unresolved.Count);
            Assert.AreEqual("item_fuel", c.ResolveItemOrMissing(new ContentId("item_fuel"), "x").IdString);
            c.ClearUnresolved();
            Assert.AreEqual(0, c.Unresolved.Count);
        }

        [Test]
        public void FormerId_ResolvesToLiveDefinition()
        {
            var defs = Placeholders();
            var renamed = Item("item_new_name", so => { var p = so.FindProperty("formerIds"); p.arraySize = 1; p.GetArrayElementAtIndex(0).stringValue = "item_old_name"; });
            defs.Add(renamed);
            var c = ContentCatalog.Build(defs, new ValidationReport("test"));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Log, new System.Text.RegularExpressions.Regex("former ID"));
            Assert.IsTrue(c.TryGet(new ContentId("item_old_name"), out var def)); Assert.AreSame(renamed, def);
            Assert.AreSame(renamed, c.ResolveItemOrMissing(new ContentId("item_old_name"), "save"));
            Assert.IsFalse(c.TryGet(new ContentId("item_old_name").Hash, out _), "hash lookups use live IDs only");
        }

        [Test]
        public void IdentityHash_IsOrderIndependent()
        {
            var defs = Placeholders(); defs.Add(Item("item_a")); defs.Add(Block("block_b", BlockKind.Structural));
            ulong h1 = ContentCatalog.Build(defs, new ValidationReport("t")).IdentityHash;
            var reversed = Enumerable.Reverse(defs).ToList();
            Assert.AreEqual(h1, ContentCatalog.Build(reversed, new ValidationReport("t")).IdentityHash);
            var changed = Placeholders(); changed.Add(Item("item_c")); changed.Add(Block("block_b", BlockKind.Structural));
            Assert.AreNotEqual(h1, ContentCatalog.Build(changed, new ValidationReport("t")).IdentityHash);
        }

        [Test]
        public void TryGetByHash_FindsDefinitions()
        {
            var defs = Placeholders(); defs.Add(Item("item_a")); defs.Add(Block("block_b", BlockKind.Structural));
            var c = ContentCatalog.Build(defs, new ValidationReport("t"));
            foreach (var d in defs)
            {
                Assert.IsTrue(c.TryGet(d.Id.Hash, out var found), d.IdString); Assert.AreSame(d, found);
            }
            Assert.IsTrue(c.TryGet<BlockDefinition>(new ContentId("block_b").Hash, out var b)); Assert.AreEqual("block_b", b.IdString);
            Assert.IsFalse(c.TryGet<BlockDefinition>(new ContentId("item_a").Hash, out _), "wrong type");
            Assert.IsFalse(c.TryGet(new ContentHash(12345), out _));
        }

        [Test]
        public void HashCollision_IsE005()
        {
            ContentCatalog.HashOverrideForTests = id => id == "item_b" || id == "item_a" ? new ContentHash(42) : new ContentHash(StableHash.Hash64(id));
            var defs = Placeholders(); defs.Add(Item("item_a")); defs.Add(Item("item_b"));
            var r = new ValidationReport("t");
            ContentCatalog.Build(defs, r);
            CollectionAssert.AreEqual(new[] { "DATA-E005" }, Codes(r), r.ToText());
            StringAssert.Contains("item_a", r.Entries[0].Message);
        }

        [Test]
        public void TryGet_DoesNotAllocate()
        {
            var defs = Placeholders(); defs.Add(Item("item_a"));
            var c = ContentCatalog.Build(defs, new ValidationReport("t"));
            var id = new ContentId("item_a"); var hash = id.Hash; var missing = new ContentId("item_zzz");
            c.TryGet(id, out _); c.TryGet<ItemDefinition>(id, out _); c.TryGet(hash, out _); c.TryGet(missing, out _);
            Assert.That(() => { for (int i = 0; i < 1000; i++) { c.TryGet(id, out _); c.TryGet<ItemDefinition>(id, out _); c.TryGet(hash, out _); c.TryGet(missing, out _); } },
                        Is.Not.AllocatingGCMemory());
        }
    }
}
