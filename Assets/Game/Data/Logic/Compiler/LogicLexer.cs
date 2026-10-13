using System;
using System.Text;

namespace Game.Data.Logic.Compiler
{
    /// <summary>SPEC-40 §4.6: ordinal ASCII tokens with one-based source locations.</summary>
    internal sealed class LogicLexer
    {
        readonly string source;
        readonly bool command;
        int position, line = 1, column = 1;
        public LogicLexer(string source, bool command = false) { this.source = source ?? ""; this.command = command; }
        char Current => position < source.Length ? source[position] : '\0';
        static bool Letter(char c) => c >= 'a' && c <= 'z';
        static bool Digit(char c) => c >= '0' && c <= '9';
        static bool Word(char c) => Letter(c) || Digit(c) || c == '_';
        void Advance()
        {
            char c = Current; position++;
            if (c == '\r') { if (Current == '\n') position++; line++; column = 1; }
            else if (c == '\n') { line++; column = 1; }
            else column++;
        }
        public LogicToken Next()
        {
            while (position < source.Length && char.IsWhiteSpace(Current)) Advance();
            int start = position, row = line, col = column;
            LogicToken Token(LogicTokenKind kind, string text = null) => new LogicToken(kind, text ?? source.Substring(start, position - start), row, col);
            LogicDiagnostics Error(string message) => new LogicDiagnostics("NAR-E001", message, row, col);
            if (position == source.Length) return Token(LogicTokenKind.End, "");
            char c = Current;
            if (c == '"')
            {
                Advance(); var value = new StringBuilder();
                while (position < source.Length && Current != '"')
                {
                    c = Current;
                    if (c == '\\')
                    {
                        Advance(); c = Current;
                        if (position == source.Length) break;
                        if (c != '"' && c != '\\') throw Error("Only quote and backslash escapes are supported.");
                    }
                    if (c == '\r') { value.Append(c); if (position + 1 < source.Length && source[position + 1] == '\n') value.Append('\n'); }
                    else value.Append(c);
                    Advance();
                }
                if (position == source.Length) throw new LogicDiagnostics("NAR-E002", "Unterminated string.", row, col);
                Advance(); return Token(LogicTokenKind.String, value.ToString());
            }
            if (c == '$')
            {
                Advance(); int name = position;
                if (!Letter(Current)) throw Error("Expected a fact name after '$'.");
                while (Word(Current)) Advance();
                if (position - name > 64) throw Error("Name exceeds 64 characters.");
                return Token(LogicTokenKind.Variable, source.Substring(name, position - name));
            }
            if (Letter(c) || Digit(c) || command && c == '_')
            {
                if (command) while (Word(Current)) Advance();
                else if (Digit(c)) while (Digit(Current)) Advance();
                else while (Word(Current)) Advance();
                string text = source.Substring(start, position - start);
                bool integer = true; foreach (char ch in text) integer &= Digit(ch);
                if (integer) return Token(LogicTokenKind.Int, text);
                if (!command && text.Length > 64) throw Error("Name exceeds 64 characters.");
                if (text == "true") return Token(LogicTokenKind.True);
                if (text == "false") return Token(LogicTokenKind.False);
                if (command) return Token(LogicTokenKind.Word);
                return Token(text == "not" ? LogicTokenKind.Not : text == "and" ? LogicTokenKind.And : text == "or" ? LogicTokenKind.Or : LogicTokenKind.Name);
            }
            Advance();
            switch (c)
            {
                case '+': return Token(LogicTokenKind.Plus);
                case '-': return Token(LogicTokenKind.Minus);
                case '(': return Token(LogicTokenKind.Open);
                case ')': return Token(LogicTokenKind.Close);
                case ',': return Token(LogicTokenKind.Comma);
                case '!': if (Current == '=') { Advance(); return Token(LogicTokenKind.NotEqual); } return Token(LogicTokenKind.Not);
                case '=': if (Current == '=') { Advance(); return Token(LogicTokenKind.Equal); } break;
                case '<': if (Current == '=') { Advance(); return Token(LogicTokenKind.LessEqual); } return Token(LogicTokenKind.Less);
                case '>': if (Current == '=') { Advance(); return Token(LogicTokenKind.GreaterEqual); } return Token(LogicTokenKind.Greater);
                case '&': if (Current == '&') { Advance(); return Token(LogicTokenKind.And); } break;
                case '|': if (Current == '|') { Advance(); return Token(LogicTokenKind.Or); } break;
            }
            throw Error("Unexpected character '" + c + "'.");
        }
    }
}
