using System.Collections.Generic;
using System.Globalization;

namespace Game.Data.Logic.Compiler
{
    /// <summary>Authoring-time parser. Stops at the first diagnostic; binding belongs to NAR-003.</summary>
    internal sealed class LogicParser
    {
        readonly LogicLexer lexer;
        LogicToken current;
        int depth;
        LogicParser(string source, bool command)
        { lexer = new LogicLexer(source, command); current = lexer.Next(); }
        LogicToken Take() { var token = current; current = lexer.Next(); return token; }
        LogicDiagnostics Error(string message) =>
            new LogicDiagnostics("NAR-E001", message, current.Line, current.Column);
        void Expect(LogicTokenKind kind)
        { if (current.Kind != kind) throw Error("Expected " + kind + "."); Take(); }
        void Enter()
        { if (++depth > 64) throw Error("expression too deep"); }
        public static LogicAst ParseExpression(string source)
        {
            var parser = new LogicParser(source, false);
            var result = parser.Or();
            parser.Expect(LogicTokenKind.End);
            return result;
        }
        public static LogicAstCommand ParseCommand(string source)
        {
            var parser = new LogicParser(source, true);
            if (parser.current.Kind == LogicTokenKind.End)
                throw new LogicDiagnostics("NAR-E013", "Empty command.", 1, 1);
            var name = parser.current;
            if (name.Text.Length == 0 || name.Text.Length > 64 ||
                name.Text[0] < 'a' || name.Text[0] > 'z' ||
                (name.Kind != LogicTokenKind.Word && name.Kind != LogicTokenKind.True && name.Kind != LogicTokenKind.False))
                throw parser.Error("Expected command name.");
            parser.Take();
            var arguments = new List<LogicAst>();
            while (parser.current.Kind != LogicTokenKind.End)
            {
                var token = parser.current;
                if (token.Kind == LogicTokenKind.Minus)
                {
                    parser.Take();
                    if (parser.current.Kind != LogicTokenKind.Int ||
                        parser.current.Line != token.Line || parser.current.Column != token.Column + 1)
                        throw parser.Error("Expected digits after minus.");
                    var digits = parser.Take();
                    if (!int.TryParse("-" + digits.Text, NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out int value))
                        throw new LogicDiagnostics("NAR-E001", "Integer out of range.", token.Line, token.Column);
                    arguments.Add(new LogicAstLiteral(new LogicToken(LogicTokenKind.Int,
                        "-" + digits.Text, token.Line, token.Column), value));
                }
                else if (token.Kind == LogicTokenKind.Variable)
                { parser.Take(); arguments.Add(new LogicAstVariable(token)); }
                else if (token.Kind == LogicTokenKind.Word || token.Kind == LogicTokenKind.String)
                { parser.Take(); arguments.Add(new LogicAstLiteral(token, token.Text)); }
                else if (token.Kind == LogicTokenKind.Int || token.Kind == LogicTokenKind.True || token.Kind == LogicTokenKind.False)
                    arguments.Add(parser.Literal());
                else throw parser.Error("Expected command argument.");
            }
            return new LogicAstCommand(name, arguments.ToArray());
        }
        LogicAst Or()
        {
            var left = And();
            while (current.Kind == LogicTokenKind.Or)
            { var op = Take(); left = new LogicAstBinary(op, left, And()); }
            return left;
        }
        LogicAst And()
        {
            var left = Not();
            while (current.Kind == LogicTokenKind.And)
            { var op = Take(); left = new LogicAstBinary(op, left, Not()); }
            return left;
        }
        LogicAst Not()
        {
            if (current.Kind != LogicTokenKind.Not) return Compare();
            Enter(); var op = Take(); var operand = Not(); depth--;
            return new LogicAstUnary(op, operand);
        }
        LogicAst Compare()
        {
            var left = Additive();
            switch (current.Kind)
            {
                case LogicTokenKind.Equal: case LogicTokenKind.NotEqual:
                case LogicTokenKind.Less: case LogicTokenKind.LessEqual:
                case LogicTokenKind.Greater: case LogicTokenKind.GreaterEqual:
                    var op = Take(); return new LogicAstBinary(op, left, Additive());
                default: return left;
            }
        }
        LogicAst Additive()
        {
            var left = Unary();
            while (current.Kind == LogicTokenKind.Plus || current.Kind == LogicTokenKind.Minus)
            { var op = Take(); left = new LogicAstBinary(op, left, Unary()); }
            return left;
        }
        LogicAst Unary()
        {
            if (current.Kind != LogicTokenKind.Minus) return Primary();
            Enter(); var op = Take(); var operand = Unary(); depth--;
            return new LogicAstUnary(op, operand);
        }
        LogicAst Primary()
        {
            var token = current;
            switch (token.Kind)
            {
                case LogicTokenKind.Int: case LogicTokenKind.True: case LogicTokenKind.False:
                    return Literal();
                case LogicTokenKind.Variable:
                    Take(); return new LogicAstVariable(token);
                case LogicTokenKind.Open:
                    Enter(); Take(); var inner = Or(); Expect(LogicTokenKind.Close); depth--; return inner;
                case LogicTokenKind.Name:
                    Enter(); Take(); Expect(LogicTokenKind.Open);
                    var arguments = new List<LogicAst>();
                    if (current.Kind != LogicTokenKind.Close)
                    {
                        while (true)
                        {
                            if (current.Kind == LogicTokenKind.String)
                            { var text = Take(); arguments.Add(new LogicAstLiteral(text, text.Text)); }
                            else arguments.Add(Or());
                            if (current.Kind != LogicTokenKind.Comma) break;
                            Take();
                        }
                    }
                    Expect(LogicTokenKind.Close); depth--;
                    return new LogicAstCall(token, arguments.ToArray());
                default: throw Error("Expected expression.");
            }
        }
        LogicAstLiteral Literal()
        {
            var token = Take();
            if (token.Kind == LogicTokenKind.True || token.Kind == LogicTokenKind.False)
                return new LogicAstLiteral(token, token.Kind == LogicTokenKind.True);
            if (!int.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                throw new LogicDiagnostics("NAR-E001", "Integer out of range.", token.Line, token.Column);
            return new LogicAstLiteral(token, value);
        }
    }
}
