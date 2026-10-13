using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration.Narrative;
using Game.Editor.Content;
using Game.Editor.Narrative;
using NUnit.Framework;
using UnityEditor;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>NAR-015: six required preservation/flow cases over the actual legacy exports.</summary>
    public sealed class LegacyNarrativeConverterTests
    {
        const string Root = "Assets/Game/Content/_Test/NAR015";
        [TearDown] public void Cleanup()
        {
            AssetDatabase.DeleteAsset(Root);
            DestroyAll();
        }
        static LegacyNarrativeConverter.Plan Prepare()
        {
            var report = new ValidationReport("NAR015 fixture");
            var sources = LegacyNarrativeLoader.Load("Import/Legacy", report);
            Assert.IsNotNull(sources, report.ToText());
            var star = Create<AuthoredStarSystemDefinition>("starsys_whisper");
            var encounter = Create<EncounterDefinition>("encounter_traveling_merchant");
            var plan = LegacyNarrativeConverter.Prepare(sources, Root, Root + "/manifest.md",
                id => id == star.IdString ? star : id == encounter.IdString ? encounter : null, report);
            Assert.IsNotNull(plan, report.ToText()); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            return plan;
        }
        [Test] public void Manifest_CoversEveryLegacyString()
        {
            var plan = Prepare();
            Assert.AreEqual(34, plan.Encounters.Count);
            Assert.AreEqual(93, plan.Encounters.Sum(e => e.Nodes.Count));
            Assert.AreEqual(188, plan.Encounters.Sum(e => e.Nodes.Sum(n => n.Options.Count)));
            int expected = 0;
            foreach (var encounter in plan.Encounters)
                foreach (var node in encounter.Nodes)
                {
                    var original = node.Source.Text.Replace("\r\n", "\n").Replace("\\n", "\n").Split('\n').Select(x => x.Trim()).ToArray();
                    string file = LegacyNarrativeConverter.DialoguePath(Root, encounter.BeatId);
                    foreach (string text in original.Concat(node.Options.Select(o => o.Source.Text.Trim())))
                    { Assert.IsTrue(plan.Manifest.Entries.Any(row => row.OutputFile == file && row.Text == text), text); expected++; }
                }
            Assert.AreEqual(expected, plan.Manifest.Entries.Count);
            foreach (var row in plan.Manifest.Entries)
            {
                string line = plan.TextFiles[row.OutputFile].Split('\n').Single(l => l.Contains(" #line:" + row.Destination + " ") || l.EndsWith(" #line:" + row.Destination, StringComparison.Ordinal));
                int marker = line.IndexOf(" #line:", StringComparison.Ordinal);
                string text = line.Substring(0, marker).TrimStart();
                if (row.Unavailable) text = text.Substring(text.IndexOf(": -> ", StringComparison.Ordinal) + 5);
                else if (text.StartsWith("-> ", StringComparison.Ordinal)) text = text.Substring(3);
                int condition = text.IndexOf(" <<if ", StringComparison.Ordinal);
                if (condition >= 0) text = text.Substring(0, condition);
                Assert.AreEqual(row.Text, Unescape(text), row.SourcePath);
            }
        }
        [Test] public void Output_IsDeterministic()
        {
            var plan = Prepare();
            Directory.CreateDirectory(Root + "/References"); AssetDatabase.Refresh();
            foreach (var pair in plan.References) AssetDatabase.CreateAsset(pair.Value, Root + "/References/" + pair.Key + ".asset");
            var report = new ValidationReport("write");
            LegacyNarrativeConverter.Write(plan, true, report);
            Recompile(Root, report);
            var before = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
            Assert.Throws<IOException>(() => LegacyNarrativeConverter.Write(plan, false, report));
            LegacyNarrativeConverter.Write(plan, true, report);
            Recompile(Root, report);
            CollectionAssert.AreEquivalent(before.Keys, Directory.GetFiles(Root, "*", SearchOption.AllDirectories));
            foreach (var pair in before) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), pair.Key);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
        }
        internal static void Recompile(string root, ValidationReport report)
        {
            var generated = AssetDatabase.FindAssets("t:ContentDefinition", new[] { root }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ContentDefinition>).ToArray();
            // Use real production dependencies, with generated definitions replacing their same-ID originals.
            var database = UnityEngine.ScriptableObject.CreateInstance<ContentDatabase>();
            try
            {
                database.EditorSetDefinitions(generated.Concat(ContentDatabaseBuilder.Rebuild().Definitions)
                    .GroupBy(d => d.IdString, StringComparer.Ordinal).Select(group => group.First())
                    .OrderBy(d => d.IdString, StringComparer.Ordinal).ToList());
                report.Merge(LogicCompilePostprocessor.RecompileAll(database, root));
                Assert.AreEqual(0, report.ErrorCount, report.ToText());
            }
            finally { UnityEngine.Object.DestroyImmediate(database); }
        }
        [Test] public void LineIds_Unique()
        {
            var plan = Prepare();
            string[] ids = plan.TextFiles.Where(p => p.Key.EndsWith(".yarn", StringComparison.Ordinal))
                .SelectMany(p => Regex.Matches(p.Value, @"(?<!\\)#line:([^\s]+)").Cast<Match>().Select(m => m.Groups[1].Value)).ToArray();
            Assert.AreEqual(plan.Manifest.Entries.Count, ids.Length);
            Assert.AreEqual(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        }
        [Test] public void PoolOptions_Commented()
        {
            var plan = Prepare();
            var rows = plan.Manifest.Entries.Where(e => e.Unavailable).ToArray(); Assert.AreEqual(1, rows.Length);
            foreach (var row in rows)
            {
                string line = plan.TextFiles[row.OutputFile].Split('\n').Single(l => l.Contains("#line:" + row.Destination));
                StringAssert.StartsWith("// LEGACY-UNAVAILABLE PLAY_FROM_POOL(", line);
            }
            foreach (string line in plan.TextFiles.Where(p => p.Key.EndsWith(".yarn", StringComparison.Ordinal)).SelectMany(p => p.Value.Split('\n')))
                if (line.Contains("PLAY_FROM_POOL")) StringAssert.StartsWith("// LEGACY-UNAVAILABLE", line);
        }
        [Test] public void TerminalCommands_EndTheirOption()
        {
            var plan = Prepare(); int checkedCommands = 0;
            foreach (var file in plan.TextFiles.Where(p => p.Key.EndsWith(".yarn", StringComparison.Ordinal)))
            {
                string[] lines = file.Value.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimStart();
                    if (!(line.StartsWith("<<open_ship_editor", StringComparison.Ordinal) || line.StartsWith("<<start_combat ", StringComparison.Ordinal) || line.StartsWith("<<request_lane_jump ", StringComparison.Ordinal))) continue;
                    checkedCommands++;
                    int indent = lines[i].Length - line.Length;
                    for (int j = i + 1; j < lines.Length && !string.IsNullOrWhiteSpace(lines[j]); j++)
                    {
                        int nextIndent = lines[j].Length - lines[j].TrimStart().Length;
                        if (nextIndent < indent) break;
                        Assert.Fail("Statement after terminal command: " + file.Key + ":" + (j + 1));
                    }
                }
            }
            Assert.GreaterOrEqual(checkedCommands, 3);
        }
        [Test] public void NestedEncounter_UsesReturn()
        {
            var plan = Prepare(); Assert.IsTrue(plan.Nested.Contains("beat_shady_man"));
            var nested = plan.Encounters.Single(e => e.BeatId == "beat_shady_man");
            string text = plan.TextFiles[LegacyNarrativeConverter.DialoguePath(Root, nested.BeatId)];
            StringAssert.Contains("<<return>>", text); StringAssert.DoesNotContain("<<stop>>", text);
            StringAssert.Contains("<<complete_beat beat_shady_man>>", text);
            string hub = plan.TextFiles[LegacyNarrativeConverter.DialoguePath(Root, "beat_station_hub")];
            StringAssert.Contains("<<detour " + nested.Nodes[0].Title + ">>", hub);
            StringAssert.Contains("visited_count(\"" + nested.Nodes[0].Title + "\") == 0", hub);
        }
        static string Unescape(string value)
        {
            var output = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            { if (value[i] == '\\' && i + 1 < value.Length) i++; output.Append(value[i]); }
            return output.ToString();
        }
    }
}
