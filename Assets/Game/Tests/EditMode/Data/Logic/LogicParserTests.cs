using Game.Data.Logic.Compiler;
using NUnit.Framework;

namespace Game.Tests.EditMode.Data.Logic
{
    [Category("Narrative")]
    public sealed class LogicParserTests
    {
        [Test]
        public void Precedence_MatchesGrammar()
        {
            var root = (LogicAstBinary)LogicParser.ParseExpression("not $a and $b or $c");
            Assert.AreEqual(LogicTokenKind.Or, root.Operator);
            var and = (LogicAstBinary)root.Left;
            Assert.AreEqual(LogicTokenKind.And, and.Operator);
            Assert.AreEqual(LogicTokenKind.Not, ((LogicAstUnary)and.Left).Operator);
            Assert.AreEqual("c", ((LogicAstVariable)root.Right).Name);
            var comparison = (LogicAstBinary)LogicParser.ParseExpression("1 + 2 >= 3");
            Assert.AreEqual(LogicTokenKind.GreaterEqual, comparison.Operator);
            Assert.AreEqual(LogicTokenKind.Plus, ((LogicAstBinary)comparison.Left).Operator);
        }

        [Test]
        public void Synonyms_Accepted()
        {
            var root = (LogicAstBinary)LogicParser.ParseExpression("!$a && $b || $c");
            Assert.AreEqual(LogicTokenKind.Or, root.Operator);
            var and = (LogicAstBinary)root.Left;
            Assert.AreEqual(LogicTokenKind.And, and.Operator);
            Assert.AreEqual(LogicTokenKind.Not, ((LogicAstUnary)and.Left).Operator);
        }

        [Test]
        public void Examples_Parse()
        {
            Assert.IsInstanceOf<LogicAstBinary>(LogicParser.ParseExpression(
                "has_item(\"resource_palladium\",3) and not $gate_ark_start_active"));
            Assert.IsInstanceOf<LogicAstBinary>(LogicParser.ParseExpression(
                "since_accept(\"defeated_encounter_xeno_probe_tutorial\") >= 1"));
            Assert.AreEqual("take_item", LogicParser.ParseCommand("take_item resource_palladium 3").Name);
            Assert.AreEqual("set_fact", LogicParser.ParseCommand("set_fact gate_ark_start_active true").Name);
            Assert.AreEqual("notify", LogicParser.ParseCommand("notify \"Gate online.\"").Name);
        }

        [Test]
        public void SyntaxError_ReportsLineColumn()
        {
            var error = Assert.Throws<LogicDiagnostics>(() => LogicParser.ParseExpression("has_item(\"x\" 3)"));
            Assert.AreEqual("NAR-E001", error.Code);
            Assert.AreEqual(1, error.Line);
            Assert.AreEqual(14, error.Column);
            error = Assert.Throws<LogicDiagnostics>(() => LogicParser.ParseExpression("true and\r\n  )"));
            Assert.AreEqual(2, error.Line);
            Assert.AreEqual(3, error.Column);
            Assert.AreEqual("NAR-E013", Assert.Throws<LogicDiagnostics>(() => LogicParser.ParseCommand("  ")).Code);
        }

        [Test]
        public void StringEscapes()
        {
            var command = LogicParser.ParseCommand("notify \"say \\\"hi\\\"\"");
            Assert.AreEqual("say \"hi\"", ((LogicAstLiteral)command.Arguments[0]).Value);
            command = LogicParser.ParseCommand("notify \"a\\\\b\"");
            Assert.AreEqual("a\\b", ((LogicAstLiteral)command.Arguments[0]).Value);
            Assert.AreEqual("NAR-E002", Assert.Throws<LogicDiagnostics>(
                () => LogicParser.ParseCommand("notify \"unfinished")).Code);
        }

        [Test]
        public void DepthLimit_Enforced()
        {
            Assert.IsInstanceOf<LogicAstLiteral>(LogicParser.ParseExpression(new string('(', 64) + "1" + new string(')', 64)));
            var error = Assert.Throws<LogicDiagnostics>(() =>
                LogicParser.ParseExpression(new string('(', 65) + "1" + new string(')', 65)));
            Assert.AreEqual("NAR-E001", error.Code);
            Assert.AreEqual("expression too deep", error.Message);
        }

        [Test]
        public void Command_ParsesArgsAndSignedInts()
        {
            var command = LogicParser.ParseCommand("example resource_fuel -3 7 true false \"text\" $story_fact -2147483648");
            Assert.AreEqual(8, command.Arguments.Count);
            Assert.AreEqual("resource_fuel", ((LogicAstLiteral)command.Arguments[0]).Value);
            Assert.AreEqual(-3, ((LogicAstLiteral)command.Arguments[1]).Value);
            Assert.AreEqual(7, ((LogicAstLiteral)command.Arguments[2]).Value);
            Assert.AreEqual(true, ((LogicAstLiteral)command.Arguments[3]).Value);
            Assert.AreEqual(false, ((LogicAstLiteral)command.Arguments[4]).Value);
            Assert.AreEqual("text", ((LogicAstLiteral)command.Arguments[5]).Value);
            Assert.AreEqual("story_fact", ((LogicAstVariable)command.Arguments[6]).Name);
            Assert.AreEqual(int.MinValue, ((LogicAstLiteral)command.Arguments[7]).Value);
        }
    }
}
