using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;
using Game.Editor.Migration.Narrative;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>NAR-014: five required checks; bounded export traversal, no simulation or wall-clock waiting.</summary>
    public sealed class LegacyActionTranslatorTests
    {
        static string LegacyRoot => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Import", "Legacy"));
        [TearDown] public void Cleanup() => DestroyAll();

        [Test] public void AllLegacyActions_Mapped()
        {
            var sources = Load();
            var translator = new LegacyActionTranslator(sources);
            var logic = AllLogic(sources, translator, out var converted);
            Assert.AreEqual(34, converted.Count);
            Assert.AreEqual(93, converted.Sum(e => e.Nodes.Count));
            Assert.AreEqual(188, converted.Sum(e => e.Nodes.Sum(n => n.Options.Count)));
            Assert.AreEqual(11, sources.Quests.Length); Assert.AreEqual(4, sources.Triggers.Length); Assert.AreEqual(3, sources.Templates.Length);
            Assert.Greater(sources.Placements.BodyEncounterLinks.Count, 0);
            Assert.IsTrue(logic.All(o => o.Statements.All(s => !s.Contains("Unmapped"))));
            Assert.Greater(translator.Logs.Count, 0);
            Assert.IsTrue(translator.Logs.All(l => !string.IsNullOrEmpty(l.Text)), "log text survives separately from the logic command");
            Assert.AreEqual(translator.Logs.Count, translator.Logs.Select(l => l.QuestId + "/" + l.Id).Distinct().Count());
            var gate = converted.Single(e => e.Source.Id == "story_activate_gate"); Assert.AreEqual("beat_gate_dormant", gate.BeatId);
            Assert.AreEqual("beat_gate_active", converted.Single(e => e.Source.Id == "story_use_active_gate").BeatId);
            Assert.AreEqual("beat_station_hub", converted.Single(e => e.Source.Id == "demo_station_hub").BeatId);
            var nora = converted.Single(e => e.Source.Id == "story_waking_up").Nodes.First();
            Assert.AreSame(nora.Source, sources.Encounters.Single(e => e.Id == "story_waking_up").Pages[0]);
            foreach (var encounter in converted)
                for (int i = 0; i < encounter.Nodes.Count; i++)
                {
                    var node = encounter.Nodes[i];
                    CollectionAssert.AreEqual(encounter.Source.Pages[i].Tags, node.Tags);
                    Assert.AreEqual(LegacyNarrativeMapping.NodeTitle(encounter.Source.Id, node.Source.Id), node.Title);
                    for (int j = 0; j < node.Options.Count; j++) Assert.AreSame(node.Source.Options[j], node.Options[j].Source);
                }
            var pool = converted.SelectMany(e => e.Nodes).SelectMany(n => n.Options).Single(o => o.Source.Actions.StartsWith("PLAY_FROM_POOL", StringComparison.Ordinal));
            Assert.IsTrue(pool.Unavailable); StringAssert.StartsWith("// LEGACY-UNAVAILABLE", pool.Statements.Single()); Assert.AreEqual(0, pool.Commands.Count);
            Assert.Throws<FormatException>(() => translator.Requirement("REQ_UNKNOWN(1)"));
            Assert.Throws<FormatException>(() => Translate("UNKNOWN_ACTION"));
            Assert.Throws<FormatException>(() => Translate("GOTO(missing)"));
            // Strict reading must abort the complete model on a newly introduced nested JSON property.
            string root = Path.Combine(Path.GetTempPath(), "NAR014_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "narrative"));
                foreach (string file in Directory.GetFiles(Path.Combine(LegacyRoot, "narrative"), "*.json")) File.Copy(file, Path.Combine(root, "narrative", Path.GetFileName(file)));
                string target = Path.Combine(root, "narrative", "NarrativeEvents.json");
                string json = File.ReadAllText(target); int at = json.IndexOf("\"text\":", StringComparison.Ordinal);
                Assert.Greater(at, 0); json = json.Insert(at, "\"UnexpectedField\":true,"); File.WriteAllText(target, json);
                var report = new ValidationReport("strict narrative"); Assert.IsNull(LegacyNarrativeLoader.Load(root, report));
                Assert.IsTrue(report.Entries.Any(e => e.Code == "MIG-E002" && e.Field.Contains("UnexpectedField")), report.ToText());
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test] public void EmittedLogic_Compiles()
        {
            var sources = Load(); var translator = new LegacyActionTranslator(sources);
            var logic = AllLogic(sources, translator, out var converted);
            var definitions = FactCatalogTests.BaseContent();
            definitions.Add(FactCatalogTests.Facts("facts_legacy", translator.Facts.ToArray()));
            var content = FactCatalogTests.Content(definitions);
            // Other converters have not produced their assets yet: SPEC-40 explicitly permits NAR-W001.
            var refs = new RefResolverRegistry(); var report = new ValidationReport("legacy logic");
            var facts = FactCatalog.Build(content, refs, report); var compiler = new LogicCompiler(facts, refs, new LogicFunctionRegistry());
            int commands = 0, conditions = 0;
            foreach (var option in logic)
            {
                foreach (string command in option.Commands)
                {
                    Assert.IsTrue(compiler.TryCompileCommand(command, Options(LogicUsage.DialogCommand), report, out _), command + "\n" + report.ToText()); commands++;
                }
                foreach (string condition in option.Conditions)
                {
                    Assert.IsTrue(compiler.TryCompileExpression(condition, Options(LogicUsage.DialogCondition), report, out _), condition + "\n" + report.ToText()); conditions++;
                }
                // Yarn control words belong to SPEC-41; check their targets against this model, while
                // their expressions and all vocabulary commands above go through LogicCompiler.
                foreach (string statement in option.Statements)
                {
                    if (statement.StartsWith("<<jump ", StringComparison.Ordinal) || statement.StartsWith("<<detour ", StringComparison.Ordinal))
                    {
                        string target = statement.Substring(statement.IndexOf(' ') + 1); target = target.Substring(0, target.Length - 2);
                        Assert.IsTrue(converted.Any(e => e.Nodes.Any(n => n.Title == target)), statement);
                    }
                }
            }
            foreach (var node in converted.SelectMany(e => e.Nodes))
                foreach (string statement in node.Statements)
                {
                    string command = statement.Substring(2, statement.Length - 4);
                    Assert.IsTrue(compiler.TryCompileCommand(command, Options(LogicUsage.DialogCommand), report, out _), report.ToText()); commands++;
                }
            Assert.Greater(commands, 200); Assert.Greater(conditions, 30); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            // complete_quest has its own documented warning (NAR-W010); no other warning is allowed.
            Assert.IsTrue(report.Entries.All(e => e.Code == "NAR-W001" || e.Code == "NAR-W010"), report.ToText());
        }

        [Test] public void RandomBranch_CumulativeThresholds()
        {
            var result = Translate("RANDOM_BRANCH(50:a|30:b|20:c)");
            CollectionAssert.AreEqual(new[] { "<<if roll(100) <= 50>>", "<<jump fixture_a>>", "<<elseif roll(100) <= 80>>", "<<jump fixture_b>>", "<<else>>", "<<jump fixture_c>>", "<<endif>>" }, result.Statements);
            CollectionAssert.AreEqual(new[] { "roll(100) <= 50", "roll(100) <= 80" }, result.Conditions);
            Assert.IsTrue(result.HasFlow); Assert.AreEqual(0, result.Commands.Count);
            var scaled = Translate("RANDOM_BRANCH(5:a|3:b|2:c)"); CollectionAssert.AreEqual(result.Statements, scaled.Statements);
            Assert.Throws<FormatException>(() => Translate("RANDOM_BRANCH(0:a|1:b)"));
        }

        [Test] public void ImplicitCosts_BecomeConditions()
        {
            var result = Translate("REMOVE_ITEM(500, 3); CHANGE_CURRENCY(-50); CHANGE_FUEL(-2); CHANGE_CREW(-1); END(TRUE)", "REQ_VAR(has_hacking, TRUE)");
            foreach (string cost in new[] { "has_item(\"resource_palladium\", 3)", "credits() >= 50", "has_item(\"resource_fuel\", 2)", "has_item(\"resource_crew\", 1)" })
                StringAssert.Contains(cost, result.EnableCondition);
            StringAssert.Contains("$has_hacking", result.EnableCondition);
            Assert.AreEqual("", Translate("CHANGE_CURRENCY(50); CHANGE_FUEL(2); END(TRUE)").EnableCondition);
            var sources = Load(); var translator = new LegacyActionTranslator(sources); var logic = AllLogic(sources, translator, out _);
            foreach (var option in logic)
                foreach (string command in option.Commands)
                    if (command.StartsWith("take_", StringComparison.Ordinal))
                        StringAssert.Contains(LegacyActionTranslator.ImplicitCosts(new[] { command }), option.EnableCondition);
        }

        [Test] public void NumericItemIds_Mapped()
        {
            var sources = Load(); var translator = new LegacyActionTranslator(sources); var logic = AllLogic(sources, translator, out _);
            foreach (var option in logic)
            {
                foreach (string command in option.Commands.Where(c => c.StartsWith("give_item ", StringComparison.Ordinal) || c.StartsWith("take_item ", StringComparison.Ordinal)))
                { Assert.AreNotEqual("500", command.Split(' ')[1]); Assert.AreNotEqual("717185106", command.Split(' ')[1]); }
                foreach (string condition in option.Conditions)
                { Assert.IsFalse(condition.Contains("has_item(\"500\"")); Assert.IsFalse(condition.Contains("has_item(\"717185106\"")); }
            }
            CollectionAssert.AreEqual(new[] { "give_item resource_palladium 1" }, Translate("ADD_ITEM(500, 1)").Commands);
            CollectionAssert.AreEqual(new[] { "take_item item_strange_matter_container_full 1" }, Translate("REMOVE_ITEM(717185106, 1)").Commands);
            var salvage = sources.Encounters.Single(e => e.Id == "event_derelict_salvage"); var module = salvage.Pages.Single(p => p.Id == "module_found");
            var take = translator.TranslateOption(salvage, module, module.Options.Single(o => o.Text == "Take Module"));
            CollectionAssert.AreEqual(new[] { "give_loot loot_salvage_module 1", "remove_body" }, take.Commands);
            var distress = sources.Encounters.Single(e => e.Id == "event_distress_trap"); var survivors = distress.Pages.Single(p => p.Id == "real_survivors");
            var reward = translator.TranslateOption(distress, survivors, survivors.Options.Single(o => o.Actions.Contains("ADD_ITEM(500")));
            CollectionAssert.Contains(reward.Commands, "give_item item_research_blueprint 1");
            Assert.AreEqual("has_item(\"resource_palladium\", 3)", translator.Requirement("REQ_ITEM(500, 3)"));
            Assert.AreEqual("has_item(\"item_strange_matter_container_full\", 1)", translator.Requirement("REQ_ITEM(717185106, 1)"));
        }

        static LogicCompileOptions Options(LogicUsage usage) => new LogicCompileOptions(usage, "test:NAR-014", "legacy");
        static LegacyNarrativeLoader.Sources Load()
        {
            var report = new ValidationReport("legacy narrative"); var sources = LegacyNarrativeLoader.Load(LegacyRoot, report);
            Assert.IsNotNull(sources, report.ToText()); Assert.AreEqual(0, report.ErrorCount, report.ToText()); return sources;
        }
        static List<ConvertedEncounter.Option> AllLogic(LegacyNarrativeLoader.Sources sources, LegacyActionTranslator translator, out List<ConvertedEncounter> converted)
        {
            var report = new ValidationReport("legacy translation"); converted = translator.ConvertAll(report);
            Assert.IsNotNull(converted, report.ToText()); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            var result = converted.SelectMany(e => e.Nodes).SelectMany(n => n.Options).ToList();
            foreach (var quest in sources.Quests)
            {
                Actions(quest.OnAcceptedActions, quest.Id); Actions(quest.OnCompletedActions, quest.Id); Actions(quest.OnFailedActions, quest.Id);
                foreach (var objective in quest.Objectives) Condition(objective.Condition);
                foreach (var option in quest.EntityOptions)
                {
                    Condition(option.ShowCondition); Condition(option.EnableCondition);
                    foreach (var outcome in option.Outcomes) result.Add(translator.TranslateOutcome(outcome));
                }
            }
            foreach (var template in sources.Templates)
            { Actions(template.OnAcceptedActions, template.Id); Actions(template.OnCompletedActions, template.Id); }
            foreach (var trigger in sources.Triggers)
            {
                Actions(trigger.Actions, null);
                TriggerCondition(trigger.Condition);
            }
            return result;

            void Actions(LegacyQuestActionDto[] actions, string owner)
            { foreach (var action in actions) result.Add(translator.TranslateQuestAction(action, owner)); }
            void TriggerCondition(LegacyConditionDto source)
            {
                if (source.All != null) { foreach (var child in source.All) TriggerCondition(child); return; }
                if (source.Type == "event_value")
                    Assert.IsTrue(LegacyNarrativeMapping.EventPredicateRoles.ContainsKey(source.Key), source.Key);
                else Condition(source);
            }
            void Condition(LegacyConditionDto source)
            {
                string condition = translator.StructuredCondition(source);
                if (condition.Length != 0) { var output = new ConvertedEncounter.Option(); output.Conditions.Add(condition); result.Add(output); }
            }
        }
        static ConvertedEncounter.Option Translate(string actions, string enable = "")
        {
            var pages = new[] { new LegacyPageDto { Id = "start" }, new LegacyPageDto { Id = "a" }, new LegacyPageDto { Id = "b" }, new LegacyPageDto { Id = "c" } };
            var encounter = new LegacyEncounterDto { Id = "event_fixture", Pages = pages };
            var sources = new LegacyNarrativeLoader.Sources(); sources.Encounters.Add(encounter);
            return new LegacyActionTranslator(sources).TranslateOption(encounter, pages[0], new LegacyOptionDto { Text = "Fixture", Actions = actions, EnableReq = enable });
        }
    }
}
