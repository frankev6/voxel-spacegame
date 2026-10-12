using System;
using System.Collections.Generic;
using System.IO;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using NUnit.Framework;

namespace Game.Tests.EditMode.App
{
    public sealed class ScenarioParserTests
    {
        static ScenarioProbeRegistry Probes() => ScenarioProbeRegistry.CreateDefault();
        [Test] public void Precedence()
        {
            var expression = ScenarioParser.ParseExpression("!true == false && 1 + 2 * 3 == 7 || false", Probes());
            Assert.IsTrue(ScenarioExpression.RequireBool(expression.Evaluate(null)));
            Assert.AreEqual(3, ScenarioExpression.RequireNumber(ScenarioParser.ParseExpression("-(1 + 2) + 12 / 2", Probes()).Evaluate(null)));
        }
        [Test] public void Strings_Escapes()
        {
            var script = ScenarioParser.Parse("scenario escapes\ndesc \"quote \\\" and slash \\\\\"\nassert \"a#b\" == \"a#b\" \"keep # in quotes\" # drop comment\n", DevCommandRegistry.CreateDefault(), Probes());
            Assert.AreEqual("quote \" and slash \\", script.Description);
            Assert.AreEqual("keep # in quotes", script.Statements[0].Message);
            Assert.IsTrue(ScenarioExpression.RequireBool(script.Statements[0].Expression.Evaluate(null)));
        }
        [Test] public void HeaderOrder_Error()
        {
            var error = Assert.Throws<FormatException>(() => ScenarioParser.Parse("# comment\nstatus active\nscenario invalid", DevCommandRegistry.CreateDefault(), Probes()));
            StringAssert.Contains("line 2, column 1", error.Message); StringAssert.Contains("scenario must be first", error.Message);
        }
        [Test] public void UnknownProbe_ErrorWithLine()
        {
            var error = Assert.Throws<FormatException>(() => ScenarioParser.Parse("scenario unknown\n\nassert missing.probe == 1", DevCommandRegistry.CreateDefault(), Probes()));
            StringAssert.Contains("line 3, column 8", error.Message); StringAssert.Contains("unknown probe 'missing.probe'", error.Message);
        }
        sealed class FutureCommand : IDevCommand
        {
            public string Name { get; } public string Usage => Name; public string Summary => "parse-only fixture";
            internal FutureCommand(string name) { Name = name; }
            public DevCommandResult Execute(DevArgs args, DevContext ctx) => throw new NotSupportedException("parse fixture only");
        }
        sealed class FutureProbe : IScenarioProbe
        {
            public string Name { get; } public int ArgCount => 0;
            internal FutureProbe(string name) { Name = name; }
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args) => throw new NotSupportedException("parse fixture only");
        }
        [Test] public void R28Example_Parses()
        {
            string spec = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "../docs/specs/SPEC-70-devtools-harness.md"));
            int start = spec.IndexOf("scenario r28_pause_load", StringComparison.Ordinal), end = spec.IndexOf("```", start, StringComparison.Ordinal);
            var commands = new List<IDevCommand>(DevCommandRegistry.CreateDefault().Commands);
            foreach (string name in new[] { "teleport", "save" }) commands.Add(new FutureCommand(name));
            var probes = Probes();
            foreach (string name in new[] { "player.upos.x", "player.upos.z", "star_system" }) probes.Register(new FutureProbe(name));
            var script = ScenarioParser.Parse(spec.Substring(start, end - start), new DevCommandRegistry(commands.ToArray()), probes);
            Assert.AreEqual("r28_pause_load", script.Name); Assert.AreEqual("draft", script.Status);
            CollectionAssert.Contains(script.Tags, "R28"); Assert.AreEqual(180, script.TimeoutSeconds);
            Assert.AreEqual(21, script.Statements.Count);
            Assert.AreEqual(ScenarioStatement.StatementKind.Assert, script.Statements[script.Statements.Count - 1].Kind);
        }
    }
}
