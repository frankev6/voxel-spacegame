using System;
using System.Text;
using System.Collections.Generic;

namespace Game.Editor.Narrative.Dialog
{
    /// <summary>SPEC-41 §4.2/4.4/5.1: line-based parser and exact text escaping.</summary>
    public static partial class DialogScriptParser
    {
        public static string Escape(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' || c == '#' || c == '{' || c == '}' || c == '[' || c == ']' ||
                    c == '<' || c == '>' || c == ':' || c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                    result.Append('\\');
                result.Append(c);
            }
            return result.ToString();
        }
        public static string Unescape(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    if (++i == text.Length) throw new FormatException("Unterminated dialog escape.");
                    c = text[i];
                    if (c != '\\' && c != '#' && c != '{' && c != '}' && c != '[' && c != ']' &&
                        c != '<' && c != '>' && c != '/' && c != ':')
                        throw new FormatException("Unsupported dialog escape: \\" + c);
                }
                result.Append(c);
            }
            return result.ToString();
        }
        public static DialogScriptAst Parse(string path, string text)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (text == null) throw new ArgumentNullException(nameof(text));
            return new Parser(path, text).Run();
        }

        sealed class Parser
        {
            readonly struct SourceLine
            {
                internal readonly string Text;
                internal readonly int Number, Indent;
                internal SourceLine(string text, int number, int indent) { Text = text; Number = number; Indent = indent; }
            }
            readonly DialogScriptAst ast;
            readonly List<SourceLine> lines = new();
            readonly HashSet<string> titles = new(StringComparer.Ordinal);
            int at;
            bool End => at == lines.Count;
            SourceLine Current => lines[at];
            internal Parser(string path, string source)
            {
                ast = new DialogScriptAst { Path = path };
                var raw = source.Split('\n');
                for (int n = 0; n < raw.Length; n++)
                {
                    string line = raw[n].TrimEnd('\r');
                    if (line.IndexOf('\t') >= 0) Error(n + 1, "DLG-E011", "Tabs are forbidden; use four spaces.");
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int spaces = 0; while (spaces < line.Length && line[spaces] == ' ') spaces++;
                    if (spaces % 4 != 0) Error(n + 1, "DLG-E011", "Indentation must use multiples of four spaces.");
                    lines.Add(new SourceLine(line.Substring(spaces).TrimEnd(), n + 1, spaces / 4));
                }
            }
            void Error(int line, string code, string message) => ast.Diagnostics.Error(code, ast.Path, "line:" + line, message);
            internal DialogScriptAst Run()
            {
                while (!End)
                {
                    if (Current.Text.StartsWith("//", StringComparison.Ordinal)) { at++; continue; }
                    if (Current.Text.StartsWith("<<declare", StringComparison.Ordinal))
                    {
                        var declaration = Current; at++;
                        if (ast.Path.Replace('\\', '/').EndsWith("/_Generated/facts.yarn", StringComparison.Ordinal) && declaration.Text.EndsWith(">>", StringComparison.Ordinal))
                            ast.GeneratedDeclarations.Add(new CommandStmt { Line = declaration.Number, Text = declaration.Text.Substring(2, declaration.Text.Length - 4) });
                        else Error(declaration.Number, "DLG-E006", "declare is allowed only in _Generated/facts.yarn.");
                        continue;
                    }
                    var node = new DialogNodeAst { Line = Current.Number }; bool title = false, tags = false;
                    while (!End && Current.Text != "---" && Current.Text != "===")
                    {
                        var header = Current; at++;
                        if (header.Indent != 0) Error(header.Number, "DLG-E011", "Node headers cannot be indented.");
                        if (header.Text.StartsWith("title:", StringComparison.Ordinal) && !title)
                        { title = true; node.Title = header.Text.Substring(6).Trim(); }
                        else if (header.Text.StartsWith("tags:", StringComparison.Ordinal) && !tags)
                        { tags = true; node.Tags = Words(header.Text.Substring(5)); }
                        else Error(header.Number, "DLG-E006", "Unsupported or repeated node header: " + header.Text);
                    }
                    if (!title || !NodeName(node.Title)) Error(node.Line, "DLG-E001", "Missing or invalid node title.");
                    else if (!titles.Add(node.Title)) Error(node.Line, "DLG-E001", "Duplicate node title: " + node.Title);
                    if (End || Current.Text != "---")
                    { Error(node.Line, "DLG-E001", "Missing node body separator ---."); if (!End) at++; continue; }
                    at++; ReadBody(node.Body, 0, false); ast.Nodes.Add(node);
                    if (End || Current.Text != "===") Error(node.Line, "DLG-E001", "Missing node terminator ===.");
                    else { if (Current.Indent != 0) Error(Current.Number, "DLG-E011", "Node terminators cannot be indented."); at++; }
                }
                return ast;
            }
            static string[] Words(string text) => text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            static bool NodeName(string text)
            {
                if (string.IsNullOrEmpty(text) || text.Length > 64 || text[0] < 'a' || text[0] > 'z') return false;
                foreach (char c in text) if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_')) return false;
                return true;
            }
            static bool Branch(string text) => text == "<<else>>" || text == "<<endif>>" || text.StartsWith("<<elseif ", StringComparison.Ordinal);
            void ReadBody(List<DialogStatementAst> into, int indent, bool branch)
            {
                while (!End && Current.Text != "===")
                {
                    var line = Current;
                    if (branch && Branch(line.Text)) return;
                    if (line.Indent < indent) return;
                    if (line.Indent != indent) { Error(line.Number, "DLG-E011", "Unexpected indentation."); at++; continue; }
                    if (line.Text.StartsWith("-> ", StringComparison.Ordinal))
                    {
                        var group = new OptionGroupStmt { Line = line.Number };
                        while (!End && Current.Indent == indent && Current.Text.StartsWith("-> ", StringComparison.Ordinal))
                        {
                            var optionLine = Current; at++;
                            var option = new OptionStmt { Line = optionLine.Number };
                            ParseText(optionLine.Text.Substring(3), optionLine.Number, true, out option.Text, out _, out option.LineId, out option.Tags, out option.Condition);
                            ReadBody(option.Body, indent + 1, branch); group.Options.Add(option);
                        }
                        into.Add(group); continue;
                    }
                    at++;
                    if (line.Text.StartsWith("//", StringComparison.Ordinal)) { into.Add(new CommentStmt { Line = line.Number, Text = line.Text.Substring(2) }); continue; }
                    if (line.Text.StartsWith("=>", StringComparison.Ordinal)) { Error(line.Number, "DLG-E006", "Line groups => are forbidden."); continue; }
                    if (line.Text.StartsWith("<<if ", StringComparison.Ordinal)) { into.Add(ParseIf(line)); continue; }
                    if (Branch(line.Text)) { Error(line.Number, "DLG-E001", "Unpaired conditional branch."); continue; }
                    if (line.Text.StartsWith("<<", StringComparison.Ordinal)) { ParseCommand(line, into); continue; }
                    ParseText(line.Text, line.Number, false, out var text, out var speaker, out var id, out var tags, out _);
                    into.Add(new LineStmt { Line = line.Number, Text = text, SpeakerName = speaker, LineId = id, Tags = tags });
                }
            }
            IfStmt ParseIf(SourceLine first)
            {
                var result = new IfStmt { Line = first.Number }; var line = first;
                while (true)
                {
                    string prefix = line.Text.StartsWith("<<if ", StringComparison.Ordinal) ? "<<if " : "<<elseif ";
                    string condition = CommandInner(line, prefix.Length).Trim();
                    if (condition.Length == 0) Error(line.Number, "DLG-E001", "Conditional branches require an expression.");
                    CheckExpression(condition, line.Number);
                    var arm = new DialogIfBranch { Condition = condition }; result.Branches.Add(arm);
                    int bodyIndent = !End && !Branch(Current.Text) && Current.Indent == first.Indent + 1 ? first.Indent + 1 : first.Indent;
                    ReadBody(arm.Body, bodyIndent, true);
                    if (End || Current.Text == "===" || Current.Indent != first.Indent || !Branch(Current.Text))
                    { Error(first.Number, "DLG-E001", "Unterminated if block."); return result; }
                    line = Current; at++;
                    if (line.Text == "<<endif>>") return result;
                    if (line.Text == "<<else>>")
                    {
                        result.HasElse = true;
                        bodyIndent = !End && !Branch(Current.Text) && Current.Indent == first.Indent + 1 ? first.Indent + 1 : first.Indent;
                        ReadBody(result.Else, bodyIndent, true);
                        if (!End && Current.Indent == first.Indent && Current.Text == "<<endif>>") at++;
                        else Error(first.Number, "DLG-E001", "Else block requires endif.");
                        return result;
                    }
                }
            }
            string CommandInner(SourceLine line, int start)
            {
                if (!line.Text.EndsWith(">>", StringComparison.Ordinal)) { Error(line.Number, "DLG-E001", "Unterminated command."); return ""; }
                return line.Text.Substring(start, Math.Max(0, line.Text.Length - start - 2));
            }
            void ParseCommand(SourceLine line, List<DialogStatementAst> into)
            {
                string text = CommandInner(line, 2); int space = text.IndexOf(' ');
                string verb = space < 0 ? text : text.Substring(0, space), argument = space < 0 ? "" : text.Substring(space + 1).Trim();
                if (verb == "once" || verb == "declare" || verb == "local" || verb == "enum" || verb == "wait")
                { Error(line.Number, "DLG-E006", "Forbidden command: " + verb); return; }
                CheckExpression(text, line.Number);
                if (verb == "jump" || verb == "detour")
                {
                    if (!NodeName(argument)) { Error(line.Number, "DLG-E006", "jump/detour require a literal node name."); return; }
                    into.Add(verb == "jump" ? new JumpStmt { Line = line.Number, Node = argument } : new DetourStmt { Line = line.Number, Node = argument });
                }
                else if (verb == "return" || verb == "stop")
                {
                    if (argument.Length != 0) Error(line.Number, "DLG-E001", verb + " accepts no argument.");
                    into.Add(verb == "return" ? new ReturnStmt { Line = line.Number } : new StopStmt { Line = line.Number });
                }
                else if (verb == "set")
                {
                    int equals = argument.IndexOf('=');
                    string fact = equals < 0 ? "" : argument.Substring(0, equals).Trim();
                    string expression = equals < 0 ? "" : argument.Substring(equals + 1).Trim();
                    if (!fact.StartsWith("$", StringComparison.Ordinal) || !NodeName(fact.Substring(1)) || expression.Length == 0)
                        Error(line.Number, "DLG-E001", "set requires $fact = expression.");
                    into.Add(new SetStmt { Line = line.Number, Fact = fact.TrimStart('$'), Expression = expression });
                }
                else into.Add(new CommandStmt { Line = line.Number, Text = text });
            }
            static int Syntax(string text, string token)
            {
                for (int i = 0; i <= text.Length - token.Length; i++)
                {
                    if (text[i] == '\\') { i++; continue; }
                    if (string.CompareOrdinal(text, i, token, 0, token.Length) == 0) return i;
                }
                return -1;
            }
            void ParseText(string raw, int line, bool option, out string text, out string speaker, out string lineId, out string[] tags, out string condition)
            {
                speaker = ""; lineId = ""; condition = ""; int tag = Syntax(raw, "#");
                tags = tag < 0 ? Array.Empty<string>() : Words(raw.Substring(tag));
                if (tag >= 0) raw = raw.Substring(0, tag).TrimEnd();
                for (int i = 0; i < tags.Length; i++)
                {
                    if (!tags[i].StartsWith("#", StringComparison.Ordinal)) Error(line, "DLG-E001", "Tags must trail dialog text.");
                    tags[i] = tags[i].TrimStart('#');
                    if (tags[i].StartsWith("line:", StringComparison.Ordinal)) lineId = tags[i].Substring(5);
                    if (tags[i].StartsWith("shadow:", StringComparison.Ordinal)) Error(line, "DLG-E006", "shadow tags are forbidden.");
                }
                int inline = Syntax(raw, "<<");
                if (inline >= 0)
                {
                    if (option && raw.Substring(inline).StartsWith("<<if ", StringComparison.Ordinal) && raw.EndsWith(">>", StringComparison.Ordinal))
                    { condition = raw.Substring(inline + 5, raw.Length - inline - 7).Trim();
                        if (condition.Length == 0) Error(line, "DLG-E001", "Option conditions require an expression.");
                        CheckExpression(condition, line); raw = raw.Substring(0, inline).TrimEnd(); }
                    else Error(line, "DLG-E006", "Inline commands are forbidden except an option condition.");
                }
                foreach (string forbidden in new[] { "{", "}", "[", "]" })
                    if (Syntax(raw, forbidden) >= 0) Error(line, "DLG-E006", "Inline expressions and markup are forbidden: " + forbidden);
                try
                {
                    int colon = option ? -1 : Syntax(raw, ":");
                    if (colon >= 0 && colon + 1 < raw.Length && raw[colon + 1] == ' ')
                    { speaker = Unescape(raw.Substring(0, colon).Trim()); raw = raw.Substring(colon + 1).Trim(); }
                    text = Unescape(raw.Trim());
                }
                catch (FormatException error) { Error(line, "DLG-E001", error.Message); text = raw; }
            }
            void CheckExpression(string text, int line)
            {
                bool quoted = false;
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\\') { i++; continue; }
                    if (text[i] == '"') { quoted = !quoted; continue; }
                    if (quoted || !(char.IsLetter(text[i]) || text[i] == '_')) continue;
                    int start = i; while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    string word = text.Substring(start, i - start); int next = i;
                    while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
                    if ((word == "dice" || word == "random" || word == "random_range") && next < text.Length && text[next] == '(')
                        Error(line, "DLG-E006", "Forbidden random function: " + word);
                    i--;
                }
            }
        }
    }
}
