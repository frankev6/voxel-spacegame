using System;
using System.IO;
using System.Linq;
using Game.Editor.Narrative.Dialog;
using NUnit.Framework;

namespace Game.Tests.EditMode.Editor.Narrative
{
    public sealed class DialogScriptParserTests
    {
        static string Node(string body) => "title: test_node\n---\n" + body + "\n===\n";
        static DialogScriptAst Parse(string text)
        {
            var ast = DialogScriptParser.Parse("test.yarn", text);
            Assert.IsFalse(ast.HasErrors, ast.Diagnostics.ToText()); return ast;
        }
        [Test] public void Example_Parses()
        {
            var ast = Parse(Node("Inside are thousands of genetic samples. #line:test_00\n" +
                "-> Lie\\: 'Yes, it is safe.' #line:test_01\n    <<give_item item_seed_bank 1>>\n    <<stop>>\n" +
                "-> Truth\\: 'No, the galaxy is dying.' #line:test_02\n    <<give_item item_genetic_stimulants 1>>\n    <<stop>>"));
            Assert.AreEqual(1, ast.Nodes.Count); Assert.AreEqual(2, ast.Nodes[0].Body.Count);
            Assert.IsInstanceOf<LineStmt>(ast.Nodes[0].Body[0]);
            var group = (OptionGroupStmt)ast.Nodes[0].Body[1]; Assert.AreEqual(2, group.Options.Count);
            Assert.AreEqual("Lie: 'Yes, it is safe.'", group.Options[0].Text);
            foreach (var option in group.Options) { Assert.AreEqual(2, option.Body.Count); Assert.IsInstanceOf<CommandStmt>(option.Body[0]); Assert.IsInstanceOf<StopStmt>(option.Body[1]); }
        }
        [Test] public void Escaping_RoundTrip()
        {
            for (int i = 0; i < 200; i++)
            {
                string text = "sample" + i + " \\ #{}[]<>//: \\" + new string('#', i % 5) + " end";
                string escaped = DialogScriptParser.Escape(text);
                Assert.AreEqual(text, DialogScriptParser.Unescape(escaped));
                var line = (LineStmt)Parse(Node(escaped + " #line:test_00")).Nodes[0].Body[0];
                Assert.AreEqual(text, line.Text); Assert.AreEqual("", line.SpeakerName); Assert.AreEqual("test_00", line.LineId);
            }
        }
        [Test] public void ForbiddenFeatures_Rejected()
        {
            foreach (string body in new[] { "=> text", "<<once>>", "<<declare $x = 1>>", "<<local $x = 1>>", "<<enum Foo>>",
                "text {value} #line:x", "[bold]text[/bold] #line:x", "<<wait 1>>", "text #line:x #shadow:y",
                "<<if dice() > 0>>\n<<endif>>", "<<if random() > 0>>\n<<endif>>", "<<if random_range(1,2) > 0>>\n<<endif>>",
                "<<jump $target>>", "<<detour {target}>>" })
            {
                var ast = DialogScriptParser.Parse("test.yarn", Node(body));
                Assert.IsTrue(ast.Diagnostics.Entries.Any(e => e.Code == "DLG-E006"), body + "\n" + ast.Diagnostics.ToText());
            }
            foreach (string header in new[] { "when: true", "group: foo" })
                Assert.IsTrue(DialogScriptParser.Parse("test.yarn", "title: test_node\n" + header + "\n---\n===").Diagnostics.Entries.Any(e => e.Code == "DLG-E006"));
        }
        [Test] public void Indentation_TabsRejected()
        {
            foreach (string indent in new[] { "\t", "  " })
            {
                var ast = DialogScriptParser.Parse("test.yarn", Node(indent + "text #line:x"));
                Assert.IsTrue(ast.Diagnostics.Entries.Any(e => e.Code == "DLG-E011"));
                Assert.IsTrue(ast.Diagnostics.Entries.All(e => e.AssetPath == "test.yarn" && e.Field.StartsWith("line:", StringComparison.Ordinal)));
            }
            var separator = DialogScriptParser.Parse("test.yarn", "title: test_node\n    ---\ntext #line:x\n===");
            Assert.IsTrue(separator.Diagnostics.Entries.Any(e => e.Code == "DLG-E011"));
        }
        [Test] public void IfBlocks_Nest()
        {
            var ast = Parse(Node("<<if $first>>\n<<if $second>>\nyes #line:yes\n<<else>>\nno #line:no\n<<endif>>\n" +
                "<<elseif $third>>\n    other #line:other\n<<else>>\nlast #line:last\n<<endif>>"));
            var outer = (IfStmt)ast.Nodes[0].Body[0]; Assert.AreEqual(2, outer.Branches.Count); Assert.IsTrue(outer.HasElse);
            var inner = (IfStmt)outer.Branches[0].Body[0]; Assert.IsTrue(inner.HasElse);
            Assert.AreEqual("yes", ((LineStmt)inner.Branches[0].Body[0]).Text);
            Assert.AreEqual("other", ((LineStmt)outer.Branches[1].Body[0]).Text);
            Assert.IsTrue(DialogScriptParser.Parse("test.yarn", Node("<<if $first>>\ntext #line:x")).HasErrors);
        }
        [Test] public void SpeakerPrefix_OnlyBeforeFirstUnescapedColon()
        {
            var body = Parse(Node("NORA: First\\: second #line:one\nLiteral\\: text #line:two\n12:30 later: text #line:three\nNORA\\\\: slash #line:four")).Nodes[0].Body;
            Assert.AreEqual("NORA", ((LineStmt)body[0]).SpeakerName); Assert.AreEqual("First: second", ((LineStmt)body[0]).Text);
            Assert.AreEqual("", ((LineStmt)body[1]).SpeakerName); Assert.AreEqual("Literal: text", ((LineStmt)body[1]).Text);
            Assert.AreEqual("", ((LineStmt)body[2]).SpeakerName); Assert.AreEqual("NORA\\", ((LineStmt)body[3]).SpeakerName);
        }
        [Test] public void ConvertedLegacyFiles_ParseClean()
        {
            var files = Directory.GetFiles("Assets/Game/Content/Narrative/Dialog/Legacy", "*.yarn", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal); Assert.GreaterOrEqual(files.Length, 34);
            foreach (string file in files)
            {
                var ast = DialogScriptParser.Parse(file, File.ReadAllText(file));
                Assert.IsFalse(ast.HasErrors, ast.Diagnostics.ToText()); Assert.Greater(ast.Nodes.Count, 0, file);
            }
        }
    }
}
