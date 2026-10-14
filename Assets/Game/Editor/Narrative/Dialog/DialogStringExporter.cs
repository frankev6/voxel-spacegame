using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Game.Data.Validation;
using Game.Editor.Content;
using UnityEditor;

namespace Game.Editor.Narrative.Dialog
{
    /// <summary>SPEC-41 DLG-013: stable localization rows, including preserved unavailable legacy writing.</summary>
    public static class DialogStringExporter
    {
        public const string OutputPath = "Temp/dialog-strings.csv";
        readonly struct Row
        {
            internal readonly string Id, Node, File, Text;
            internal Row(string id, string node, string file, string text) { Id = id; Node = node; File = file; Text = text; }
        }
        public static string Build(IReadOnlyDictionary<string, string> sources)
        {
            var report = new ValidationReport("Dialog string export");
            var rows = new SortedDictionary<string, Row>(StringComparer.Ordinal);
            foreach (var script in DialogCommandTableGenerator.Parse(sources))
            {
                report.Merge(script.Diagnostics);
                foreach (var node in script.Nodes)
                    foreach (var statement in DialogSyntaxTraversal.Walk(node.Body))
                    {
                        if (statement is LineStmt line) Add(line.LineId, line.Tags, line.Text, line.Line);
                        else if (statement is OptionStmt option) Add(option.LineId, option.Tags, option.Text, option.Line);
                        else if (statement is CommentStmt comment && comment.Text.TrimStart().StartsWith("LEGACY-UNAVAILABLE ", StringComparison.Ordinal))
                        {
                            int marker = comment.Text.IndexOf(": -> ", StringComparison.Ordinal);
                            if (marker < 0) continue;
                            // Reuse the approved parser's text/tag splitting and unescaping; this AST is export-only.
                            string text = "title: " + node.Title + "\n---\n" + comment.Text.Substring(marker + 2) + "\n===\n";
                            var archived = DialogScriptParser.Parse(script.Path, text);
                            foreach (var diagnostic in archived.Diagnostics.Entries)
                                report.Add(diagnostic.Severity, diagnostic.Code, script.Path, "line:" + comment.Line, diagnostic.Message);
                            if (archived.HasErrors) continue;
                            foreach (var item in DialogSyntaxTraversal.Walk(archived.Nodes[0].Body))
                                if (item is OptionStmt preserved) Add(preserved.LineId, preserved.Tags, preserved.Text, comment.Line);
                        }

                        void Add(string id, string[] tags, string text, int sourceLine)
                        {
                            int count = 0; foreach (string tag in tags) if (tag.StartsWith("line:", StringComparison.Ordinal)) count++;
                            if (count != 1 || !Regex.IsMatch(id ?? "", "^[a-z0-9_]{1,64}$"))
                            { report.Error("DLG-E004", script.Path, "line:" + sourceLine, "Export requires exactly one valid line ID."); return; }
                            if (rows.TryGetValue(id, out var previous))
                            { report.Error("DLG-E005", script.Path, "line:" + sourceLine, "Duplicate export ID '" + id + "'; first in " + previous.File + "."); return; }
                            string file = script.Path.Replace('\\', '/');
                            if (Path.IsPathRooted(file)) file = Path.GetRelativePath(Directory.GetCurrentDirectory(), file).Replace('\\', '/');
                            rows.Add(id, new Row(id, node.Title, file, text));
                        }
                    }
            }
            if (report.HasErrors) throw new ContentValidationException(report);
            var csv = new StringBuilder("\"line id\",\"node\",\"file\",\"text\"\n");
            foreach (var row in rows.Values)
                csv.Append(Quote(row.Id)).Append(',').Append(Quote(row.Node)).Append(',').Append(Quote(row.File)).Append(',').Append(Quote(row.Text)).Append('\n');
            return csv.ToString();
        }
        static string Quote(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        public static void Export(string root = DialogCommandTableGenerator.Root, string outputPath = OutputPath)
        {
            string csv = Build(DialogCommandTableGenerator.ReadSources(root));
            DialogCommandTableGenerator.WriteText(outputPath, csv);
        }
        [MenuItem("Game/Narrative/Export Dialog Strings")]
        public static void ExportDialogStrings()
        {
            try { Export(); }
            catch (ContentValidationException exception) { ContentValidatorRunner.Log(exception.Report); throw; }
        }
    }
}
