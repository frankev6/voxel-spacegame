using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Game.Editor.Narrative.Dialog;
using NUnit.Framework;

namespace Game.Tests.EditMode.Editor.Narrative
{
    public sealed class DialogStringExporterTests
    {
        [Test] public void Export_IsDeterministic()
        {
            string root = Path.Combine(Path.GetTempPath(), "dlg013-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                string first = "title: alpha\n---\nNORA: " + DialogScriptParser.Escape("Welcome, \"captain\": #7") + " #line:c_00\n<<if true>>\n    -> Choice <<if true>> #line:a_00\n        <<stop>>\n<<endif>>\n===\n";
                string second = "title: beta\n---\nPlain #line:z_00\n// LEGACY-UNAVAILABLE PLAY_FROM_POOL(pool): -> Archived, \"choice\" #line:b_00\n===\n";
                File.WriteAllText(Path.Combine(root, "b.yarn"), second, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(root, "a.yarn"), first, new UTF8Encoding(false));
                string output = Path.Combine(root, "strings.csv");
                DialogStringExporter.Export(root, output); byte[] before = File.ReadAllBytes(output);
                DialogStringExporter.Export(root, output); CollectionAssert.AreEqual(before, File.ReadAllBytes(output));
                Assert.IsFalse(before.Length >= 3 && before[0] == 0xEF && before[1] == 0xBB && before[2] == 0xBF);
                string csv = Encoding.UTF8.GetString(before); Assert.IsFalse(csv.Contains('\r'));
                string[] rows = csv.TrimEnd('\n').Split('\n');
                Assert.AreEqual(1 + Regex.Matches(first + second, "#line:").Count, rows.Length);
                Assert.AreEqual("\"line id\",\"node\",\"file\",\"text\"", rows[0]);
                StringAssert.StartsWith("\"a_00\",\"alpha\",", rows[1]);
                StringAssert.StartsWith("\"b_00\",\"beta\",", rows[2]);
                StringAssert.StartsWith("\"c_00\",\"alpha\",", rows[3]);
                StringAssert.StartsWith("\"z_00\",\"beta\",", rows[4]);
                StringAssert.EndsWith("\"Welcome, \"\"captain\"\": #7\"", rows[3]);
                StringAssert.EndsWith("\"Archived, \"\"choice\"\"\"", rows[2]);
                Assert.IsFalse(csv.Contains("NORA:") || csv.Contains("<<if"));
                var forward = new Dictionary<string, string> { ["a.yarn"] = first, ["b.yarn"] = second };
                var reverse = new Dictionary<string, string> { ["b.yarn"] = second, ["a.yarn"] = first };
                Assert.AreEqual(DialogStringExporter.Build(forward), DialogStringExporter.Build(reverse));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
