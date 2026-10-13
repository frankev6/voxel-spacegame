using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Economy;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Economy
{
    /// <summary>SPEC-50 ECO-004: four seeded loot checks, including the required 100,000-roll distribution.</summary>
    public sealed class LootRollerTests
    {
        internal static void Entries(SerializedObject so, string path, params LootEntry[] entries)
        {
            so.FindProperty(path).arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                string row = path + ".Array.data[" + i + "]";
                TestContentFactory.Set(so, row + ".item", entries[i].item);
                TestContentFactory.Set(so, row + ".min", entries[i].min); TestContentFactory.Set(so, row + ".max", entries[i].max);
                TestContentFactory.Set(so, row + ".chancePercent", entries[i].chancePercent);
            }
        }
        internal static LootTableDefinition Table(string id, int picks, params LootEntry[] entries)
            => TestContentFactory.Create<LootTableDefinition>(id, so => { TestContentFactory.Set(so, "defaultPicks", picks); Entries(so, "entries", entries); });
        internal static LootEntry Entry(ItemDefinition item, float chance, int min = 1, int max = 1)
            => new LootEntry { item = item, chancePercent = chance, min = min, max = max };
        internal static int Count(List<(ItemDefinition item, int count)> results, ItemDefinition item)
        { foreach (var result in results) if (result.item == item) return result.count; return 0; }
        [SetUp] public void SetUp() => GameLog.ResetOnceKeys();
        [TearDown] public void TearDown() { TestContentFactory.DestroyAll(); LogAssert.NoUnexpectedReceived(); }

        [Test] public void Deterministic()
        {
            var a = TestContentFactory.Item("resource_loot_a"); var b = TestContentFactory.Item("resource_loot_b");
            var table = Table("loot_deterministic", 0, Entry(a, 37, 2, 5), Entry(b, 100, 1, 6), Entry(a, 100, 3, 9));
            var first = new List<(ItemDefinition item, int count)>(); var second = new List<(ItemDefinition item, int count)>();
            foreach (int picks in new[] { 0, 1, 7 }) foreach (ulong seed in new[] { 0UL, 1UL, 42UL, ulong.MaxValue })
            {
                first.Add((a, 99999)); second.Add((b, 88888));
                LootRoller.Roll(table, picks, seed, first); LootRoller.Roll(table, picks, seed, second);
                CollectionAssert.AreEqual(first, second); Assert.That(first.Count, Is.InRange(1, 2));
                Assert.LessOrEqual(Count(first, a) + Count(first, b), picks == 0 ? 20 : picks * 9, "Roll replaces the caller's old results.");
            }
            var merged = Table("loot_merged", 0, Entry(b, 100, 2, 2), Entry(a, 100, 3, 3), Entry(b, 100, 4, 4));
            LootRoller.Roll(merged, 0, 42, first); Assert.AreEqual(2, first.Count);
            Assert.AreEqual((b, 6), first[0]); Assert.AreEqual((a, 3), first[1]);
            var repeated = Table("loot_repeated", 4, Entry(a, 100, 2, 2));
            LootRoller.Roll(repeated, -1, 42, first); Assert.AreEqual(1, first.Count); Assert.AreEqual((a, 8), first[0]);
            LootRoller.Roll(repeated, 2, 42, first); Assert.AreEqual((a, 4), first[0]);
        }
        [Test] public void AppendEntry_StableEarlier()
        {
            var a = TestContentFactory.Item("resource_append_a"); var b = TestContentFactory.Item("resource_append_b"); var c = TestContentFactory.Item("resource_append_c");
            var original = Table("loot_original", 0, Entry(a, 100, 1, 5), Entry(b, 1, 1, 9));
            var appended = Table("loot_appended", 0, Entry(a, 100, 1, 5), Entry(b, 1, 1, 9), Entry(c, 100, 2, 7));
            var changedChance = Table("loot_changed_chance", 0, Entry(a, 100, 1, 5), Entry(b, 100, 1, 9), Entry(c, 100, 2, 7));
            var before = new List<(ItemDefinition item, int count)>(); var after = new List<(ItemDefinition item, int count)>(); var changed = new List<(ItemDefinition item, int count)>();
            for (ulong seed = 0; seed < 128; seed++)
            {
                LootRoller.Roll(original, 0, seed, before); LootRoller.Roll(appended, 0, seed, after); LootRoller.Roll(changedChance, 0, seed, changed);
                Assert.AreEqual(Count(before, a), Count(after, a)); Assert.AreEqual(Count(before, b), Count(after, b));
                Assert.AreEqual(Count(after, c), Count(changed, c), "A failed chance still consumes its quantity draw, preserving later rows.");
            }
        }
        [Test] public void Picks_Distribution()
        {
            var items = new[] { TestContentFactory.Item("resource_weight_a"), TestContentFactory.Item("resource_weight_b"), TestContentFactory.Item("resource_weight_c") };
            var table = Table("loot_weights", 1, Entry(items[0], 20), Entry(items[1], 30), Entry(items[2], 50));
            var results = new List<(ItemDefinition item, int count)>(3); var counts = new int[3]; int malformed = 0;
            const int rolls = 100000;
            for (ulong seed = 0; seed < rolls; seed++)
            {
                LootRoller.Roll(table, 1, seed, results);
                if (results.Count != 1 || results[0].count != 1) { malformed++; continue; }
                int index = Array.IndexOf(items, results[0].item); if (index < 0) malformed++; else counts[index]++;
            }
            Assert.AreEqual(0, malformed); Assert.AreEqual(rolls, counts[0] + counts[1] + counts[2]);
            var expected = new[] { 20000, 30000, 50000 };
            for (int i = 0; i < counts.Length; i++) Assert.That(counts[i], Is.EqualTo(expected[i]).Within(expected[i] * .02));
            TestContext.WriteLine($"ECO-004 weighted picks: {rolls} seeds, counts={counts[0]}/{counts[1]}/{counts[2]}; 0 gameplay ticks/waits.");
        }
        [Test] public void EmptyTable_Warns()
        {
            var item = TestContentFactory.Item("resource_empty_test"); var results = new List<(ItemDefinition item, int count)> { (item, 999) };
            var empty = Table("loot_empty", 0);
            LogAssert.Expect(LogType.Warning, "[Economy] Loot table 'loot_empty' has no rollable entries.");
            LootRoller.Roll(empty, 0, 0, results); Assert.IsEmpty(results);
            LootRoller.Roll(empty, 2, 1, results); Assert.IsEmpty(results);
            var zero = Table("loot_zero_weight", 2, Entry(item, 0));
            LogAssert.Expect(LogType.Warning, "[Economy] Loot table 'loot_zero_weight' has no rollable entries.");
            LootRoller.Roll(zero, 2, 0, results); Assert.IsEmpty(results); LootRoller.Roll(zero, 0, 1, results); Assert.IsEmpty(results);
        }
    }
}
