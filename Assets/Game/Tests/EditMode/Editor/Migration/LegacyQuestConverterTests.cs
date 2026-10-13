using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration.Narrative;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>Exactly four NAR-016 checks, using actual exports and actual dependency assets.</summary>
    public sealed class LegacyQuestConverterTests
    {
        const string Root = "Assets/Game/Content/_Test/NAR016";
        [SetUp] public void SetUp() => Assert.IsFalse(Directory.Exists(Root), "Preserve unrelated test/user output.");
        [TearDown] public void Cleanup() => AssetDatabase.DeleteAsset(Root);
        static LegacyNarrativeConverter.Plan Prepare()
        {
            var report = new ValidationReport("NAR016"); var sources = LegacyNarrativeLoader.Load("Import/Legacy", report);
            Assert.IsNotNull(sources, report.ToText());
            var plan = LegacyNarrativeConverter.Prepare(sources, Root, Root + "/manifest.md", LegacyNarrativeConverter.Resolve, report, includeQuests: true);
            Assert.IsNotNull(plan, report.ToText()); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            return plan;
        }
        [Test] public void QuestCounts()
        {
            var plan = Prepare();
            Assert.AreEqual(9, plan.Quests.Quests.Count(q => q.Kind != QuestKind.Contract), "11 sources - 3 chain sources + 1 merged chain = 9 total quests.");
            Assert.AreEqual(3, plan.Quests.Quests.Count(q => q.Kind == QuestKind.Contract));
            var chain = plan.Quests.Quests.Single(q => q.Id == "quest_whisper_chain");
            CollectionAssert.AreEqual(new[] { "triangulate", "intercept", "debrief" }, chain.Stages.Select(s => s.Id));
            Assert.AreEqual(QuestKind.Debug, plan.Quests.Quests.Single(q => q.Id == "quest_debug_fail_sandbox").Kind);
            Assert.AreEqual(4, plan.Quests.Beats.Count(b => b.Trigger == BeatTrigger.Interact));
            Assert.AreEqual(2, plan.Quests.Beats.Count(b => b.Trigger == BeatTrigger.StarSystemEntered));
            Assert.IsTrue(plan.Quests.Beats.Where(b => b.Trigger == BeatTrigger.StarSystemEntered).All(b => b.FirstVisit && b.Node.Length != 0));
            Assert.AreEqual(2, plan.Manifest.Entries.Count(e => e.Dropped));
            foreach (var quest in plan.Quests.Quests)
                Assert.AreEqual(quest.Logs.Count, quest.Logs.Select(l => l.Id).Distinct().Count(), quest.Id);
        }
        [Test] public void Strings_InManifest()
        {
            var plan = Prepare();
            var strings = plan.Manifest.Entries.Where(e => e.SourcePath.StartsWith("narrative/QuestDefinitions.json:", StringComparison.Ordinal) ||
                e.SourcePath.StartsWith("narrative/MissionTemplates.json:", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(143, strings.Length);
            foreach (var entry in strings)
            {
                if (entry.OutputFile.EndsWith(".yarn", StringComparison.Ordinal))
                    StringAssert.Contains(YarnEmitter.Escape(entry.Text) + " #line:" + entry.Destination, plan.TextFiles[entry.OutputFile]);
                else
                {
                    string id = Path.GetFileNameWithoutExtension(entry.OutputFile);
                    var quest = plan.Quests.Quests.Single(q => q.Id == id);
                    var values = new[] { quest.Title, quest.Short, quest.Description }.Concat(quest.Stages.Select(s => s.Title))
                        .Concat(quest.Stages.SelectMany(s => s.Objectives).SelectMany(o => new[] { o.Text, o.Marker.label }))
                        .Concat(quest.Logs.Select(l => l.Text));
                    Assert.IsTrue(values.Any(v => v != null && v.Contains(entry.Text)), entry.SourcePath + " -> " + entry.Destination);
                }
            }
            // Existing dialogue rows and their destinations remain unchanged when quest rows are appended.
            var sources = LegacyNarrativeLoader.Load("Import/Legacy", new ValidationReport("dialogue"));
            var dialogue = LegacyNarrativeConverter.Prepare(sources, Root, Root + "/manifest.md", LegacyNarrativeConverter.Resolve, new ValidationReport("dialogue"));
            CollectionAssert.AreEqual(dialogue.Manifest.Entries.Select(e => (e.SourcePath, e.Text, e.OutputFile, e.Destination)),
                plan.Manifest.Entries.Take(dialogue.Manifest.Entries.Count).Select(e => (e.SourcePath, e.Text, e.OutputFile, e.Destination)));
            foreach (var file in dialogue.TextFiles.Where(p => p.Key.EndsWith(".yarn", StringComparison.Ordinal))) Assert.AreEqual(file.Value, plan.TextFiles[file.Key], file.Key);
        }
        [Test] public void CounterObjectives_UseBaselines()
        {
            var plan = Prepare(); int counters = 0;
            foreach (var quest in plan.Quests.Quests)
                for (int s = 0; s < quest.Stages.Count; s++)
                    foreach (var objective in quest.Stages[s].Objectives)
                    {
                        string condition = objective.Condition;
                        Assert.IsFalse(Regex.IsMatch(condition, @"\$(beat_|visited_|interacted_|defeated_|acquired_|sys_)"), condition);
                        if (condition.Contains("since_"))
                        {
                            StringAssert.Contains(s == 0 ? "since_accept(" : "since_stage(", condition); counters++;
                            StringAssert.DoesNotContain(s == 0 ? "since_stage(" : "since_accept(", condition);
                        }
                        if (condition.StartsWith("has_item(", StringComparison.Ordinal))
                        { Assert.IsFalse(objective.Latch); Assert.Greater(objective.Target, 0); StringAssert.StartsWith("item_count(", objective.Progress); }
                    }
            Assert.Greater(counters, 10);
            Assert.IsTrue(plan.Facts.Any(f => f.Name == "relation_watch_territory_seen"));
            Assert.IsTrue(plan.Quests.Quests.Single(q => q.Id == "quest_relation_watch").Stages[0].Objectives.Any(o => o.Condition.Contains("$relation_watch_territory_seen")));
        }
        [Test] public void FlagClues_Created()
        {
            var plan = Prepare(); var report = new ValidationReport("write NAR016");
            LegacyNarrativeConverter.Write(plan, true, report); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            foreach (var row in LegacyClueWriter.Texts)
            {
                var clue = AssetDatabase.LoadAssetAtPath<ClueDefinition>(Root + "/Clues/" + row.Id + ".asset");
                Assert.IsNotNull(clue, row.Id); Assert.AreEqual(row.Title, clue.Title); Assert.AreEqual(row.Body, clue.Body);
                if (row.Mystery.Length != 0) Assert.AreEqual(row.Mystery, clue.Mystery.IdString);
                if (clue is RumorDefinition rumor)
                { Assert.AreEqual(row.Target.Length == 0 ? "" : row.Target, rumor.Target == null ? "" : rumor.Target.IdString); Assert.AreEqual(row.Condition, rumor.ResolvedWhen.Source); }
            }
            foreach (var row in LegacyClueWriter.Mysteries)
            {
                var mystery = AssetDatabase.LoadAssetAtPath<MysteryDefinition>(Root + "/Clues/" + row.Id + ".asset");
                Assert.IsNotNull(mystery); CollectionAssert.AreEqual(row.Requires, mystery.Requires.Select(c => c.IdString));
            }
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<QuestDefinition>(Root + "/Quests/quest_whisper_chain.asset"));
        }
    }
}
