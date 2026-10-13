using System.Linq;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;

namespace Game.Tests.EditMode.Galaxy.Travel
{
    /// <summary>TRV-003's single required owner-usage case, with the real compiler/binder and exact indexed paths.</summary>
    public sealed class GalaxyGenerationLogicTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        [Test] public void LaneCondition_CompilesWithLaneUsage()
        {
            using var env = new LogicTestEnvironment();
            var definition = TestContentFactory.Create<GalaxyGenerationDefinition>("galaxy_logic");
            var lanes = (System.Collections.Generic.List<AuthoredLane>)definition.AuthoredLanes;
            var lane = new AuthoredLane(); lanes.Add(lane);
            LogicTestEnvironment.Source(lane.condition, "$intro_awake");
            var report = new ValidationReport("Lane usage"); definition.CompileLogic(env.Compiler, report.For("test:galaxy"));
            definition.BindLogic(new LogicBinder(env.Facts, env.Refs, report));
            Assert.IsFalse(report.HasErrors, report.ToText()); Assert.IsFalse(lane.condition.IsStale);
            var dependencies = new System.Collections.Generic.List<LogicDependency>(); lane.condition.CollectDependencies(dependencies);
            Assert.AreEqual(1, dependencies.Count); Assert.AreEqual(env.Facts.SlotOf("intro_awake"), dependencies[0].Key);
            LogicTestEnvironment.Source(lane.condition, "roll(6) == 1"); report = new ValidationReport("Forbidden lane function");
            definition.CompileLogic(env.Compiler, report.For("test:galaxy"));
            Assert.IsTrue(report.Entries.Any(entry => entry.Code == "NAR-E008" && entry.AssetPath == "test:galaxy" && entry.Field.StartsWith("authoredLanes[0].condition (", System.StringComparison.Ordinal)), report.ToText());
            Assert.IsTrue(lane.condition.HasErrors);
        }
    }
}
