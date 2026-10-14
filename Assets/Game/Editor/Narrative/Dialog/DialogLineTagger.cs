using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.Data.Validation;

namespace Game.Editor.Narrative.Dialog
{
    public static class DialogLineTagger
    {
        internal static SortedDictionary<string, string> Append(IReadOnlyDictionary<string, string> sources, ValidationReport report)
        {
            var scripts = DialogCommandTableGenerator.Parse(sources);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var titles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var script in scripts)
            {
                report.Merge(script.Diagnostics);
                foreach (var node in script.Nodes)
                {
                    if (!titles.Add(node.Title)) report.Error("DLG-E003", script.Path, "line:" + node.Line, "Duplicate node title prevents safe line tagging.");
                    foreach (var statement in DialogSyntaxTraversal.Walk(node.Body))
                    {
                        string id = statement is LineStmt line ? line.LineId : statement is OptionStmt option ? option.LineId : null;
                        string[] tags = statement is LineStmt text ? text.Tags : statement is OptionStmt choice ? choice.Tags : Array.Empty<string>();
                        int count = tags.Count(tag => tag.StartsWith("line:", StringComparison.Ordinal));
                        if (count > 1 || count == 1 && string.IsNullOrEmpty(id))
                            report.Error("DLG-E004", script.Path, "line:" + statement.Line, "Existing line tags are malformed; preserve them for manual repair.");
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!Regex.IsMatch(id, "^[a-z0-9_]{1,64}$")) report.Error("DLG-E004", script.Path, "line:" + statement.Line, "Existing line ID is invalid; preserve it for manual repair.");
                        if (!ids.Add(id)) report.Error("DLG-E005", script.Path, "line:" + statement.Line, "Duplicate line ID prevents safe line tagging: " + id + ".");
                    }
                }
            }
            var output = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (report.HasErrors) return output;
            foreach (var script in scripts)
            {
                string[] lines = sources[script.Path].Split('\n');
                foreach (var node in script.Nodes)
                {
                    int next = 0; string prefix = node.Title + "_";
                    foreach (string id in ids)
                        if (id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(id.Substring(prefix.Length), out int number) && number >= next)
                            next = checked(number + 1);
                    foreach (var statement in DialogSyntaxTraversal.Walk(node.Body))
                    {
                        string id = statement is LineStmt line ? line.LineId : statement is OptionStmt option ? option.LineId : null;
                        if (id == null || id.Length != 0) continue;
                        string assigned;
                        do { assigned = prefix + next.ToString("D2", System.Globalization.CultureInfo.InvariantCulture); next = checked(next + 1); } while (ids.Contains(assigned));
                        if (assigned.Length > 64)
                        { report.Error("DLG-E004", script.Path, "line:" + statement.Line, "Node title is too long for a valid appended line ID; choose a shorter title."); continue; }
                        ids.Add(assigned);
                        string original = lines[statement.Line - 1]; int end = original.Length;
                        while (end > 0 && char.IsWhiteSpace(original[end - 1])) end--;
                        lines[statement.Line - 1] = original.Substring(0, end) + " #line:" + assigned + original.Substring(end);
                    }
                }
                output.Add(script.Path, string.Join("\n", lines));
            }
            return output;
        }
        public static ValidationReport Tag(string root = DialogCommandTableGenerator.Root)
        {
            var report = new ValidationReport("Dialog line tagging");
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            { report.Error("DLG-E013", root, "", "Line tagging requires Edit mode."); return report; }
            var sources = DialogCommandTableGenerator.ReadSources(root);
            var output = Append(sources, report);
            if (report.HasErrors) return report;
            foreach (var pair in output)
            {
                if (sources[pair.Key] == pair.Value) continue;
                byte[] bytes = File.ReadAllBytes(pair.Key);
                bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                File.WriteAllText(pair.Key, pair.Value, new UTF8Encoding(bom));
            }
            UnityEditor.AssetDatabase.Refresh(); return report;
        }
    }
}
