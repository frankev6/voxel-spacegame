using System;
using System.Collections.Generic;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using NUnit.Framework;

namespace Game.Tests.EditMode.App
{
    public sealed class ScenarioEvalTests
    {
        [Test] public void MixedKinds_RuntimeError()
        {
            var expr = ScenarioParser.ParseExpression("\"1\" == 1", ScenarioProbeRegistry.CreateDefault());
            StringAssert.Contains("cannot compare String with Number", Assert.Throws<InvalidOperationException>(() => expr.Evaluate(null)).Message);
        }
        [Test] public void LetAndVariable()
        {
            var script = ScenarioParser.Parse("scenario variables\nlet x = max(3, abs(-4))\nlet y = distance(0, 0, 3, $x)\nassert min($x, $y) == 4", DevCommandRegistry.CreateDefault(), ScenarioProbeRegistry.CreateDefault());
            var variables = new Dictionary<string, ScenarioValue>();
            for (int i = 0; i < 2; i++) { var step = script.Statements[i]; variables[step.Name] = step.Expression.Evaluate(null, variables); }
            Assert.AreEqual(5, variables["y"].Number);
            Assert.IsTrue(ScenarioExpression.RequireBool(script.Statements[2].Expression.Evaluate(null, variables)));
        }
    }
}
