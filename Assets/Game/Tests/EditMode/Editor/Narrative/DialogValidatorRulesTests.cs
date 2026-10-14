using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Validation;
using Game.Editor.Narrative;
using Game.Editor.Narrative.Dialog;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using UnityEngine;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Narrative
{
    public sealed class DialogValidatorRulesTests
    {
        [Serializable] sealed class Ysls { public YslsRow[] commands = null, functions = null; }
        [Serializable] sealed class YslsRow { public string yarnName = null; public YslsParam[] parameters = null; public YslsReturn @return = null; }
        [Serializable] sealed class YslsParam { public string name = null; }
        [Serializable] sealed class YslsReturn { public string type = null; }
        const string Root = "Assets/Game/Content/_Test/DLG003";
        readonly List<ContentDefinition> definitions = new();
        [SetUp] public void SetUp()
        {
            Assert.IsFalse(Directory.Exists(Root), "Preserve unrelated output.");
            definitions.AddRange(FactCatalogTests.BaseContent());
            definitions.Add(FactCatalogTests.Facts("facts_dialog", new FactDeclaration { Name = "story_number", Kind = FactKind.Int },
                new FactDeclaration { Name = "story_flag", Kind = FactKind.Bool }));
            definitions.Add(TestContentFactory.Create<StoryBeatDefinition>("beat_dialog", so => TestContentFactory.Set(so, "yarnNode", "root")));
            definitions.Add(TestContentFactory.Create<SpeakerDefinition>("speaker_nora", so => TestContentFactory.Set(so, "displayName", "NORA")));
            definitions.Add(TestContentFactory.Create<IllustrationDefinition>("illustration_known"));
        }
        [TearDown] public void TearDown()
        {
            AssetDatabase.DeleteAsset(Root); definitions.Clear(); TestContentFactory.DestroyAll();
            Game.Editor.Content.ContentDatabaseBuilder.Rebuild();
        }
        NarrativeValidationContext Context()
        {
            var report = new ValidationReport("fixture");
            var ctx = DialogCommandTableGenerator.Context(FactCatalogTests.Content(definitions), report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText()); return ctx;
        }
        static string Node(string title, string body) => "title: " + title + "\ntags: \n---\n" + body + "\n===\n";
        static Dictionary<string, string> Sources(string body, string other = null)
        {
            var result = new Dictionary<string, string> { [Root + "/main.yarn"] = Node("root", body) };
            if (other != null) result.Add(Root + "/other.yarn", other);
            return result;
        }
        ValidationReport Validate(Dictionary<string, string> sources)
        {
            var report = new ValidationReport("fixture");
            new DialogValidatorRules().Validate(Context(), DialogCommandTableGenerator.Parse(sources), report); return report;
        }
        [Test] public void EachRule_Fixture()
        {
            const string line = "Hello #line:root_00";
            var pairs = new List<(string code, Dictionary<string, string> fail, Dictionary<string, string> pass)>
            {
                ("DLG-E001", new Dictionary<string,string> { [Root + "/main.yarn"] = "title: root\n---\nHello" }, Sources(line)),
                ("DLG-E002", Sources("<<jump absent>>"), Sources("<<jump next>>", Node("next", "Next #line:next_00"))),
                ("DLG-E003", Sources(line, Node("root", "Other #line:other_00")), Sources("<<jump other>>", Node("other", "Other #line:other_00"))),
                ("DLG-E004", Sources("Hello"), Sources(line)),
                ("DLG-E005", Sources(line, Node("other", "Other #line:root_00")), Sources("<<jump other>>\n" + line, Node("other", "Other #line:other_00"))),
                ("DLG-E006", Sources("<<once>>"), Sources(line)),
                ("DLG-E007", Sources("<<not_a_command>>"), Sources("<<give_credits 1>>")),
                ("DLG-E008", Sources("<<if credits()>>\n" + line + "\n<<endif>>"), Sources("<<if true>>\n" + line + "\n<<endif>>")),
                ("DLG-E009", Sources("<<set $unknown = 1>>\n<<set $sys_clue_counter = 1>>\n<<set $story_number = true>>"), Sources("<<set $story_number = 1>>\n<<set $story_flag = true>>")),
                ("DLG-E010", Sources("UNKNOWN: Hello #line:root_00 #portrait:speaker_absent #speaker:speaker_absent #illustration:illustration_absent"),
                    Sources("NORA: Hello #line:root_00 #portrait:speaker_nora #speaker:speaker_nora #illustration:illustration_known")),
                ("DLG-E011", Sources("\tHello #line:root_00"), Sources(line)),
                ("DLG-E012", Sources("<<give_credits  1 >>"), Sources("<<notify \"two  spaces\">>")),
                ("DLG-E014", Sources("<<open_ship_editor>>\n" + line), Sources("<<open_ship_editor>>")),
                ("DLG-W020", Sources(Options(10, false)), Sources(Options(9, false))),
                ("DLG-W021", Sources(Options(2, true)), Sources(Options(2, false))),
                ("DLG-W022", Sources(line, Node("other", "Other #line:other_00")), Sources(line + "\n<<detour other>>", Node("other", "Other #line:other_00"))),
                ("DLG-I030", Sources(line + " #draft"), Sources(line))
            };
            foreach (var pair in pairs)
            {
                var bad = Validate(pair.fail); Assert.IsTrue(bad.Entries.Any(entry => entry.Code == pair.code), pair.code + "\n" + bad.ToText());
                var good = Validate(pair.pass); Assert.IsFalse(good.Entries.Any(entry => entry.Code == pair.code), pair.code + "\n" + good.ToText());
                Assert.AreEqual(0, good.ErrorCount, pair.code + "\n" + good.ToText());
            }
            var sources = Sources("<<give_credits 1>>"); var generated = Generate(sources); definitions.Add(generated);
            var ctx = Context(); var plan = DialogCommandTableGenerator.Prepare(ctx, sources, Root);
            var current = new ValidationReport("current"); DialogValidatorRules.CheckGenerated(ctx, plan, Root, current);
            Assert.AreEqual(0, current.ErrorCount, current.ToText());
            File.AppendAllText(Root + "/_Generated/facts.yarn", "// stale\n");
            var stale = new ValidationReport("stale"); DialogValidatorRules.CheckGenerated(ctx, plan, Root, stale);
            Assert.IsTrue(stale.Entries.Any(entry => entry.Code == "DLG-E013"));
            Assert.AreEqual(10, new DialogValidatorRules().Order);
            // Q13: validation preserves the outer visibility branch and the independent inline availability.
            var ast = DialogScriptParser.Parse(Root + "/visibility.yarn", Node("root", "<<if $story_flag>>\n    -> Hidden <<if credits() > 0>> #line:root_00\n<<endif>>"));
            var conditional = (IfStmt)ast.Nodes[0].Body[0]; var option = ((OptionGroupStmt)conditional.Branches[0].Body[0]).Options[0];
            var visibilityReport = new ValidationReport("visibility"); new DialogValidatorRules().Validate(Context(), new[] { ast }, visibilityReport);
            Assert.AreEqual(0, visibilityReport.ErrorCount, visibilityReport.ToText());
            Assert.AreEqual("$story_flag", conditional.Branches[0].Condition); Assert.AreEqual("credits() > 0", option.Condition);
        }
        static string Options(int count, bool conditional)
            => string.Join("\n", Enumerable.Range(0, count).Select(i => "-> Option " + i + (conditional ? " <<if true>>" : "") + " #line:root_" + i.ToString("D2")));
        DialogCommandTableDefinition Generate(Dictionary<string, string> sources)
        {
            Directory.CreateDirectory(Root);
            foreach (var pair in sources) File.WriteAllText(pair.Key, pair.Value, new UTF8Encoding(false));
            var report = new ValidationReport("generation");
            DialogCommandTableGenerator.Generate(Context(), sources, Root, report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            var table = AssetDatabase.LoadAssetAtPath<DialogCommandTableDefinition>(Root + "/_Generated/dialog_command_table.asset");
            Assert.IsNotNull(table); return table;
        }
        [Test] public void Generator_IsIdempotent()
        {
            var sources = Sources("<<notify \"two  spaces\">>\n<<give_credits 1>>");
            var table = Generate(sources); definitions.Add(table);
            var before = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
            var again = Generate(sources); Assert.AreSame(table, again); Assert.IsFalse(EditorUtility.IsDirty(table));
            CollectionAssert.AreEquivalent(before.Keys, Directory.GetFiles(Root, "*", SearchOption.AllDirectories));
            foreach (var pair in before) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), pair.Key);
            string facts = File.ReadAllText(Root + "/_Generated/facts.yarn");
            StringAssert.Contains("<<declare $story_flag = false>>", facts); StringAssert.Contains("<<declare $story_number = 0>>", facts);
            Assert.IsFalse(facts.Contains("$sys_"));
            var ysls = JsonUtility.FromJson<Ysls>(File.ReadAllText(Root + "/_Generated/logic.ysls.json"));
            var registry = new LogicFunctionRegistry();
            foreach (var info in registry.Snapshot().Where(info => (info.AllowedUsage & (info.IsEffect ? LogicUsage.DialogCommand : LogicUsage.DialogCondition)) != 0))
            {
                var row = (info.IsEffect ? ysls.commands : ysls.functions).Single(item => item.yarnName == info.Name);
                Assert.AreEqual(info.Params.Length, row.parameters.Length);
                Assert.AreEqual(!info.IsEffect, row.@return?.type != null);
            }
            var snapshot = registry.Snapshot(); snapshot[0].Name = "changed"; snapshot[0].Params = Array.Empty<LogicParamInfo>();
            Assert.IsFalse(registry.Snapshot().Any(info => info.Name == "changed"));
        }
        [Test] public void Tagger_AppendsOnly()
        {
            string input = "title: root\r\ntags: \r\n---\r\nA #line:root_00\r\n-> B #line:root_01\r\n    Untagged\\: text  \r\n// Untagged comment\r\n===\r\n";
            var sources = new Dictionary<string, string> { [Root + "/main.yarn"] = input };
            var report = new ValidationReport("tag"); var output = DialogLineTagger.Append(sources, report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Assert.AreEqual(input.Replace("    Untagged\\: text  \r\n", "    Untagged\\: text #line:root_02  \r\n"), output[Root + "/main.yarn"]);
            var repeated = DialogLineTagger.Append(output, new ValidationReport("again"));
            Assert.AreEqual(output[Root + "/main.yarn"], repeated[Root + "/main.yarn"]);
        }
        [Test] public void CommandTable_ContainsEveryCommand()
        {
            var sources = Sources("<<give_credits 1>>\n-> Choice #line:root_00\n    <<if true>>\n    <<give_credits 2>>\n    <<else>>\n    <<notify \"two  spaces\">>\n    <<endif>>\n    <<give_credits 1>>\n    <<set $story_number = 1>>\n    <<stop>>");
            var table = Generate(sources);
            CollectionAssert.AreEqual(new[] { "give_credits 1", "give_credits 2", "notify \"two  spaces\"" }, table.Entries.Select(entry => entry.Text));
            foreach (var entry in table.Entries)
            { Assert.IsFalse(entry.Effect.IsStale); Assert.IsFalse(entry.Effect.HasErrors); Assert.IsFalse(entry.Effect.IsEmpty); CollectionAssert.AreEqual(new[] { entry.Text }, entry.Effect.Source); }
            var report = new ValidationReport("bind"); var ctx = Context(); table.BindLogic(new LogicBinder(ctx.Facts, ctx.Refs, report));
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
        }
    }
}
