using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Editor.Migration.Narrative
{
    /// <summary>SPEC-40 §5.8 / SPEC-41 §4: deterministic text-only emission before any asset writes.</summary>
    internal static class YarnEmitter
    {
        internal readonly struct PreservedLine
        {
            public readonly string SourcePath, Text, LineId;
            public readonly bool Unavailable;
            internal PreservedLine(string path, string text, string id, bool unavailable)
            { SourcePath = path; Text = text; LineId = id; Unavailable = unavailable; }
        }
        internal sealed class Result
        {
            public string Text;
            public readonly List<PreservedLine> Lines = new();
        }
        static readonly HashSet<string> LeaveTexts = new(StringComparer.Ordinal) {
            "Leave", "Ignore", "Ignore Signal", "Leave them", "Leave Quickly", "Walk Away",
            "Undock", "Back", "Back to Ship", "Run Silent", "Continue Patrol"
        };

        internal static string Escape(string text)
        {
            var output = new StringBuilder();
            text = (text ?? "").Trim();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ("\\#{}[]<>:".IndexOf(c) >= 0 || c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                    output.Append('\\');
                output.Append(c);
            }
            return output.ToString();
        }

        internal static Result Emit(ConvertedEncounter encounter, bool nested)
        {
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));
            var result = new Result();
            var output = new StringBuilder();
            for (int pageIndex = 0; pageIndex < encounter.Nodes.Count; pageIndex++)
            {
                var node = encounter.Nodes[pageIndex];
                string path = encounter.Source.SourcePath + "/" + encounter.Source.Id + "/pages[" + pageIndex + "]";
                int sequence = 0;
                string NextId() => node.Title + "_" + (sequence++).ToString("D2", CultureInfo.InvariantCulture);
                output.Append("title: ").Append(node.Title).Append('\n');
                output.Append("tags: ").AppendJoin(' ', node.Tags).Append("\n---\n");
                foreach (string statement in node.Statements) output.Append(statement).Append('\n');
                string imageTag = ImageTag(node.Source.Image);
                string[] lines = node.Source.Text.Replace("\r\n", "\n").Replace("\\n", "\n").Split('\n');
                for (int line = 0; line < lines.Length; line++)
                {
                    string id = NextId();
                    output.Append(Escape(lines[line])).Append(" #line:").Append(id).Append(imageTag).Append('\n');
                    result.Lines.Add(new PreservedLine(path + "/text[" + line + "]", lines[line].Trim(), id, false));
                }
                for (int optionIndex = 0; optionIndex < node.Options.Count; optionIndex++)
                {
                    var option = node.Options[optionIndex];
                    string id = NextId();
                    result.Lines.Add(new PreservedLine(path + "/options[" + optionIndex + "]/text", option.Source.Text.Trim(), id, option.Unavailable));
                    if (option.Unavailable)
                    {
                        // Retain the source option and its line identity, but never emit executable pool navigation.
                        string marker = option.Statements[0];
                        int textStart = marker.IndexOf(": -> ", StringComparison.Ordinal);
                        output.Append(marker.Substring(0, textStart + 5)).Append(Escape(option.Source.Text)).Append(" #line:").Append(id).Append('\n');
                        continue;
                    }
                    bool hidden = !string.IsNullOrEmpty(option.ShowCondition);
                    string indent = hidden ? "    " : "";
                    if (hidden) output.Append("<<if ").Append(option.ShowCondition).Append(">>\n");
                    string condition = option.EnableCondition;
                    if (option.Source.Consume)
                    {
                        string target = null;
                        foreach (string statement in option.Statements)
                            if (statement.StartsWith("<<jump ", StringComparison.Ordinal) || statement.StartsWith("<<detour ", StringComparison.Ordinal))
                            { target = statement.Substring(statement.IndexOf(' ') + 1).TrimEnd('>'); break; }
                        if (target == null) throw new FormatException(path + ": consumed option has no target node.");
                        string visit = "visited_count(\"" + target + "\") == 0";
                        condition = string.IsNullOrEmpty(condition) ? visit : "(" + condition + ") and (" + visit + ")";
                    }
                    output.Append(indent).Append("-> ").Append(Escape(option.Source.Text));
                    if (!string.IsNullOrEmpty(condition)) output.Append(" <<if ").Append(condition).Append(">>");
                    output.Append(" #line:").Append(id);
                    if (option.Source.Style == "quest") output.Append(" #quest");
                    if (LeaveTexts.Contains(option.Source.Text)) output.Append(" #leave");
                    output.Append('\n');
                    bool aborted = false;
                    foreach (string original in option.Statements)
                    {
                        string statement = original;
                        if (nested && statement == "<<abort_beat>>") { aborted = true; continue; }
                        if (nested && statement == "<<stop>>")
                        {
                            if (!aborted) output.Append(indent).Append("    <<complete_beat ").Append(encounter.BeatId).Append(">>\n");
                            statement = "<<return>>";
                        }
                        output.Append(indent).Append("    ").Append(statement).Append('\n');
                    }
                    if (!option.HasFlow && !option.Terminal)
                    {
                        bool revisit = option.Commands.Exists(c => c.StartsWith("open_merchant ", StringComparison.Ordinal) || c == "repair_ship 100");
                        output.Append(indent).Append("    <<").Append(revisit ? "jump " + node.Title : "stop").Append(">>\n");
                    }
                    if (hidden) output.Append("<<endif>>\n");
                }
                output.Append("===\n\n");
            }
            result.Text = output.ToString();
            return result;
        }
        static string ImageTag(string image)
        {
            if (string.IsNullOrEmpty(image)) return "";
            if (image == "nora") return " #portrait:speaker_nora";
            if (image == "image_shady_man") return " #portrait:speaker_shady_man";
            if (image.StartsWith("image_", StringComparison.Ordinal)) return " #illustration:illustration_" + image.Substring(6);
            throw new FormatException("Unmapped legacy page image: " + image);
        }
    }
}
