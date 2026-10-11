#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using static Game.App.DevRuntime.Scenarios.ScenarioTokenizer;
using static Game.App.DevRuntime.Scenarios.ScenarioStatement;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Hand-written line and recursive-descent expression parser (SPEC-70 §6.2).</summary>
    public static class ScenarioParser
    {
        sealed class Expressions
        {
            readonly List<Token> tokens; readonly ScenarioProbeRegistry probes; readonly int line;
            int position;
            internal Token Current => tokens[position];
            internal Expressions(string text, int line, int column, ScenarioProbeRegistry probes) { tokens = Scan(text, line, column); this.line = line; this.probes = probes; }
            internal bool Take(string text) { if (Current.Kind != Kind.Symbol && Current.Kind != Kind.Name || Current.Text != text) return false; position++; return true; }
            internal Token Next() { var t = Current; if (t.Kind != Kind.End) position++; return t; }
            internal void Require(string text) { if (!Take(text)) throw Error(line, Current.Column, "expected '" + text + "'"); }
            internal void End() { if (Current.Kind != Kind.End) throw Error(line, Current.Column, "unexpected token '" + Current.Text + "'"); }
            internal ScenarioExpression Parse() => Or();
            ScenarioExpression Or() { var v = And(); while (Take("||")) v = new ScenarioExpression.Binary("||", v, And()); return v; }
            ScenarioExpression And() { var v = Compare(); while (Take("&&")) v = new ScenarioExpression.Binary("&&", v, Compare()); return v; }
            ScenarioExpression Compare()
            {
                var v = Sum(); string op = Current.Text;
                if (Current.Kind == Kind.Symbol && (op == "==" || op == "!=" || op == "<" || op == "<=" || op == ">" || op == ">=")) { Next(); v = new ScenarioExpression.Binary(op, v, Sum()); }
                return v;
            }
            ScenarioExpression Sum() { var v = Term(); while (Current.Kind == Kind.Symbol && (Current.Text == "+" || Current.Text == "-")) { string op = Next().Text; v = new ScenarioExpression.Binary(op, v, Term()); } return v; }
            ScenarioExpression Term() { var v = Unary(); while (Current.Kind == Kind.Symbol && (Current.Text == "*" || Current.Text == "/")) { string op = Next().Text; v = new ScenarioExpression.Binary(op, v, Unary()); } return v; }
            ScenarioExpression Unary()
            {
                if (Take("!")) return new ScenarioExpression.Unary("!", Unary());
                if (Take("-")) return new ScenarioExpression.Unary("-", Unary());
                return Primary();
            }
            ScenarioExpression Primary()
            {
                var token = Next();
                if (token.Kind == Kind.Number) return new ScenarioExpression.Literal(ScenarioExpression.Number(double.Parse(token.Text, CultureInfo.InvariantCulture)));
                if (token.Kind == Kind.String) return new ScenarioExpression.Literal(ScenarioExpression.Text(token.Text));
                if (token.Kind == Kind.Symbol && token.Text == "(") { var v = Parse(); Require(")"); return v; }
                if (token.Kind == Kind.Symbol && token.Text == "$")
                {
                    var name = Next(); if (name.Kind != Kind.Name) throw Error(line, name.Column, "expected variable name");
                    return new ScenarioExpression.Variable(name.Text);
                }
                if (token.Kind != Kind.Name) throw Error(line, token.Column, "expected expression");
                if (token.Text == "true" || token.Text == "false") return new ScenarioExpression.Literal(ScenarioExpression.Boolean(token.Text == "true"));
                string path = token.Text;
                while (Take(".")) { var part = Next(); if (part.Kind != Kind.Name) throw Error(line, part.Column, "expected probe name"); path += "." + part.Text; }
                if (!probes.TryGet(path, out var probe)) throw Error(line, token.Column, "unknown probe '" + path + "'");
                var args = new List<ScenarioExpression>();
                if (Take("("))
                {
                    if (!Take(")")) { do { args.Add(Parse()); } while (Take(",")); Require(")"); }
                }
                if (args.Count != probe.ArgCount) throw Error(line, token.Column, "probe '" + path + "' expects " + probe.ArgCount + " arguments");
                return new ScenarioExpression.Probe(probe, args.ToArray());
            }
        }
        public static ScenarioExpression ParseExpression(string text, ScenarioProbeRegistry probes, int line = 1, int column = 1)
        {
            var parser = new Expressions(text, line, column, probes); var expr = parser.Parse(); parser.End(); return expr;
        }
        static bool Name(string text) => Regex.IsMatch(text, "^[a-z0-9_]+$");
        static double Positive(string text, int line, int column)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) || double.IsInfinity(n) || double.IsNaN(n) || n <= 0)
                throw Error(line, column, "expected positive finite seconds");
            return n;
        }
        static long Count(string text, int line, int column)
        {
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long n)) throw Error(line, column, "expected non-negative integer");
            return n;
        }
        public static ScenarioScript Parse(string source, DevCommandRegistry commands, ScenarioProbeRegistry probes)
        {
            var script = new ScenarioScript(); var statements = new List<ScenarioStatement>(); var tags = new List<string>(); var headers = new HashSet<string>();
            string[] lines = (source ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int line = i + 1; string raw = WithoutComment(lines[i]); int leading = raw.Length - raw.TrimStart().Length;
                string text = raw.Trim(); if (text.Length == 0) continue;
                int split = 0; while (split < text.Length && !char.IsWhiteSpace(text[split])) split++;
                string word = text.Substring(0, split), rest = text.Substring(split).TrimStart();
                int restColumn = leading + text.Length - rest.Length + 1;
                if (script.Name == null && word != "scenario") throw Error(line, leading + 1, "scenario must be first");
                bool header = word == "scenario" || word == "desc" || word == "status" || word == "tags" || word == "timeout";
                if (header)
                {
                    if (statements.Count != 0) throw Error(line, leading + 1, "headers must precede steps");
                    if (!headers.Add(word)) throw Error(line, leading + 1, "duplicate header '" + word + "'");
                    switch (word)
                    {
                        case "scenario": if (!Name(rest)) throw Error(line, restColumn, "invalid scenario name"); script.Name = rest; break;
                        case "desc":
                            var tokens = Scan(rest, line, restColumn);
                            if (tokens.Count != 2 || tokens[0].Kind != Kind.String) throw Error(line, restColumn, "desc expects one quoted string");
                            script.Description = tokens[0].Text; break;
                        case "status": if (rest != "active" && rest != "draft") throw Error(line, restColumn, "status expects active or draft"); script.Status = rest; break;
                        case "tags":
                            foreach (var tag in rest.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                            { if (!Regex.IsMatch(tag, "^[a-zA-Z0-9_]+$")) throw Error(line, restColumn, "invalid tag"); tags.Add(tag); }
                            if (tags.Count == 0) throw Error(line, restColumn, "tags expects names"); break;
                        case "timeout": script.TimeoutSeconds = Positive(rest, line, restColumn); break;
                    }
                    continue;
                }
                var statement = new ScenarioStatement { Line = line, Text = text };
                switch (word)
                {
                    case "wait":
                        if (rest.StartsWith("ticks=", StringComparison.Ordinal) || rest.StartsWith("frames=", StringComparison.Ordinal))
                        {
                            int equals = rest.IndexOf('='); statement.Kind = rest[0] == 't' ? StatementKind.WaitTicks : StatementKind.WaitFrames;
                            statement.Count = Count(rest.Substring(equals + 1), line, restColumn + equals + 1); break;
                        }
                        statement.Kind = StatementKind.WaitCondition;
                        var wait = new Expressions(rest, line, restColumn, probes); statement.Expression = wait.Parse();
                        int expressionLength = wait.Current.Column - restColumn; statement.ExpressionText = rest.Substring(0, expressionLength).TrimEnd();
                        if (wait.Take("timeout")) { wait.Require("="); var seconds = wait.Next(); if (seconds.Kind != Kind.Number) throw Error(line, seconds.Column, "expected timeout seconds"); statement.TimeoutSeconds = Positive(seconds.Text, line, seconds.Column); }
                        wait.End(); break;
                    case "assert":
                        statement.Kind = StatementKind.Assert; var assertion = new Expressions(rest, line, restColumn, probes);
                        statement.Expression = assertion.Parse(); statement.ExpressionText = rest.Substring(0, assertion.Current.Column - restColumn).TrimEnd();
                        if (assertion.Current.Kind == Kind.String) statement.Message = assertion.Next().Text;
                        assertion.End(); break;
                    case "let":
                        statement.Kind = StatementKind.Let;
                        int at = rest.IndexOf('='); if (at <= 0) throw Error(line, restColumn, "let expects name = expression");
                        statement.Name = rest.Substring(0, at).Trim(); if (!Name(statement.Name) || char.IsDigit(statement.Name[0])) throw Error(line, restColumn, "invalid variable name");
                        statement.ExpressionText = rest.Substring(at + 1); statement.Expression = ParseExpression(statement.ExpressionText, probes, line, restColumn + at + 1); break;
                    case "expect_errors": statement.Kind = StatementKind.ExpectErrors; statement.Count = Count(rest, line, restColumn); break;
                    default:
                        statement.Kind = StatementKind.Command;
                        if (!commands.TryGet(word, out _)) throw Error(line, leading + 1, "unknown command '" + word + "'");
                        if (!DevArgs.TryParse(text, out _, out _, out string error)) throw Error(line, leading + 1, error);
                        break;
                }
                statements.Add(statement);
            }
            if (script.Name == null) throw Error(1, 1, "scenario must be first");
            script.Tags = tags.AsReadOnly(); script.Statements = statements.AsReadOnly(); return script;
        }
    }
}
#endif
