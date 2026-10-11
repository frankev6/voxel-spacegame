#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Location-preserving expression tokens and quoted comment handling (SPEC-70 §6.2).</summary>
    public static class ScenarioTokenizer
    {
        internal enum Kind : byte { End, Name, Number, String, Symbol }
        internal readonly struct Token
        {
            internal readonly Kind Kind;
            internal readonly string Text;
            internal readonly int Column;
            internal Token(Kind kind, string text, int column) { Kind = kind; Text = text; Column = column; }
        }
        public static string WithoutComment(string line)
        {
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '\\' && quoted && i + 1 < line.Length) { i++; continue; }
                if (line[i] == '"') quoted = !quoted;
                else if (line[i] == '#' && !quoted) return line.Substring(0, i);
            }
            return line;
        }
        internal static FormatException Error(int line, int column, string text) => new FormatException("line " + line + ", column " + column + ": " + text);
        internal static List<Token> Scan(string text, int line, int column = 1)
        {
            var tokens = new List<Token>(); int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                int start = i++;
                if (c == '"')
                {
                    var value = new StringBuilder(); bool closed = false;
                    while (i < text.Length)
                    {
                        c = text[i++];
                        if (c == '"') { closed = true; break; }
                        if (c == '\\')
                        {
                            if (i == text.Length) break;
                            c = text[i++];
                            if (c != '"' && c != '\\') throw Error(line, column + i - 1, "only quote and backslash escapes are supported");
                        }
                        value.Append(c);
                    }
                    if (!closed) throw Error(line, column + start, "unterminated quote");
                    tokens.Add(new Token(Kind.String, value.ToString(), column + start)); continue;
                }
                if (c >= '0' && c <= '9' || c == '.' && i < text.Length && char.IsDigit(text[i]))
                {
                    while (i < text.Length && char.IsDigit(text[i])) i++;
                    if (c != '.' && i < text.Length && text[i] == '.') { i++; while (i < text.Length && char.IsDigit(text[i])) i++; }
                    if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
                    {
                        i++; if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;
                        while (i < text.Length && char.IsDigit(text[i])) i++;
                    }
                    string number = text.Substring(start, i - start);
                    if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) || double.IsInfinity(n))
                        throw Error(line, column + start, "invalid finite number");
                    tokens.Add(new Token(Kind.Number, number, column + start)); continue;
                }
                if (c >= 'a' && c <= 'z' || c == '_')
                {
                    while (i < text.Length && (text[i] >= 'a' && text[i] <= 'z' || char.IsDigit(text[i]) || text[i] == '_')) i++;
                    tokens.Add(new Token(Kind.Name, text.Substring(start, i - start), column + start)); continue;
                }
                if ("()$.,+-*/!<>=&|".IndexOf(c) < 0) throw Error(line, column + start, "unexpected character '" + c + "'");
                string symbol = c.ToString();
                if (i < text.Length && ((c == '&' || c == '|') && text[i] == c || "!<>= ".IndexOf(c) >= 0 && text[i] == '=')) symbol += text[i++];
                tokens.Add(new Token(Kind.Symbol, symbol, column + start));
            }
            tokens.Add(new Token(Kind.End, "", column + text.Length)); return tokens;
        }
    }
}
#endif
