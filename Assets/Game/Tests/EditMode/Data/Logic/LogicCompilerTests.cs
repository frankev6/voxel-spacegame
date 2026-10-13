using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;
using NUnit.Framework;

namespace Game.Tests.EditMode.Data.Logic
{
    [Category("Narrative")]
    public sealed class LogicCompilerTests
    {
        static LogicCompileOptions Options(LogicUsage usage = LogicUsage.BeatCondition) => new LogicCompileOptions(usage, "test:logic", "condition");

        [Test] public void ErrorCodes_EachFixture()
        {
            using var env = new LogicTestEnvironment();
            var observed = new HashSet<string>();
            void Expression(string text, string expected, LogicUsage usage = LogicUsage.BeatCondition)
            {
                var report = new ValidationReport(text);
                Assert.IsFalse(env.Compiler.TryCompileExpression(text, Options(usage), report, out _));
                CollectionAssert.Contains(report.Entries.Select(x => x.Code).ToArray(), expected, text);
                foreach (var entry in report.Entries) observed.Add(entry.Code);
            }
            void Command(string text, string expected)
            {
                var report = new ValidationReport(text);
                Assert.IsFalse(env.Compiler.TryCompileCommand(text, Options(LogicUsage.DialogCommand), report, out _));
                CollectionAssert.Contains(report.Entries.Select(x => x.Code).ToArray(), expected, text);
                foreach (var entry in report.Entries) observed.Add(entry.Code);
            }
            Expression("(", "NAR-E001"); Expression("has_item(\"x)", "NAR-E002");
            Expression("mystery_function()", "NAR-E003"); Expression("has_item()", "NAR-E004");
            Expression("has_item(true)", "NAR-E005"); Expression("$story_absent", "NAR-E006");
            Expression("has_item(\"resource_absent\")", "NAR-E007"); Expression("roll(6) == 1", "NAR-E008");
            Expression("1", "NAR-E009"); Expression("true + 1", "NAR-E010");
            Command("set_fact sys_clue_counter 2", "NAR-E011"); Command("repair_ship 101", "NAR-E012");
            Command("", "NAR-E013"); Command("unknown_command", "NAR-E016"); Command("set_fact intro_awake 2", "NAR-E019");
            var stale = new CompiledCondition(); var staleReport = new ValidationReport("stale"); stale.Validate(staleReport);
            foreach (var entry in staleReport.Entries) observed.Add(entry.Code);
            var definitions = FactCatalogTests.BaseContent();
            definitions.Add(FactCatalogTests.Facts("facts_errors",
                new FactDeclaration { Name = "BadName" }, new FactDeclaration { Name = "sys_reserved" },
                new FactDeclaration { Name = "story_duplicate" }, new FactDeclaration { Name = "story_duplicate" },
                new FactDeclaration { Name = "story_invalid_bool", Kind = FactKind.Bool, Default = 2 },
                new FactDeclaration { Name = "story_note", Flags = FactFlags.ReadByCode }));
            var content = FactCatalogTests.Content(definitions); var factReport = new ValidationReport("facts");
            FactCatalog.Build(content, RefResolverRegistry.CreateDefault(content), factReport, _ => 7UL);
            foreach (var entry in factReport.Entries) observed.Add(entry.Code);
            for (int code = 1; code <= 22; code++) CollectionAssert.Contains(observed, "NAR-E" + code.ToString("D3"));
        }

        [Test] public void UsageRestrictions()
        {
            using var env = new LogicTestEnvironment();
            foreach (string expression in new[] { "since_accept(\"story_count\") > 0", "roll(6) == 1" })
            {
                var report = new ValidationReport(expression);
                Assert.IsFalse(env.Compiler.TryCompileExpression(expression, Options(), report, out _));
                CollectionAssert.Contains(report.Entries.Select(x => x.Code).ToArray(), "NAR-E008");
            }
            var commandReport = new ValidationReport("combat");
            Assert.IsFalse(env.Compiler.TryCompileCommand("start_combat encounter_unknown", Options(LogicUsage.QuestEffect), commandReport, out _));
            CollectionAssert.Contains(commandReport.Entries.Select(x => x.Code).ToArray(), "NAR-E008");
        }

        [Test] public void Staleness_TracksSource()
        {
            using var env = new LogicTestEnvironment();
            var condition = LogicTestEnvironment.Source(new CompiledCondition(), "true");
            Assert.IsTrue(env.Compiler.Compile(condition, Options(), new ValidationReport("first")));
            Assert.IsFalse(condition.IsStale);
            Assert.IsTrue(env.Compiler.Compile(condition, Options(), new ValidationReport("second")));
            Assert.IsFalse(condition.IsStale);
            LogicTestEnvironment.Source(condition, "false"); Assert.IsTrue(condition.IsStale);
            var integer = LogicTestEnvironment.Source(new CompiledIntExpr(), "1 + 2");
            Assert.IsTrue(env.Compiler.Compile(integer, Options(), new ValidationReport("int"))); Assert.IsFalse(integer.IsStale);
            var effects = LogicTestEnvironment.Source(new CompiledEffectList(), new[] { "notify \"hello\"" });
            Assert.IsTrue(env.Compiler.Compile(effects, Options(LogicUsage.DialogCommand), new ValidationReport("effects")));
            Assert.IsFalse(effects.IsStale);
        }

        [Test] public void Binding_ResolvesSlotsAndIndices()
        {
            using var env = new LogicTestEnvironment(); var report = new ValidationReport("binding");
            var binder = new LogicBinder(env.Facts, env.Refs, report);
            Assert.AreEqual(env.Facts.SlotOf("intro_awake"), binder.BindFact("intro_awake"));
            Assert.AreEqual(env.Content.IndexOf(env.Content.Get<ItemDefinition>(new ContentId("resource_fuel"))),
                binder.BindRef(RefKind.Item, "resource_fuel"));
            Assert.AreEqual(0, binder.FailedBindings); Assert.AreEqual(0, report.ErrorCount);
        }

        [Test] public void Dependencies_Collected()
        {
            using var env = new LogicTestEnvironment(); var report = new ValidationReport("dependencies");
            var wrapper = LogicTestEnvironment.Source(new CompiledCondition(), "has_item(\"resource_fuel\",2) and $intro_awake");
            Assert.IsTrue(env.Compiler.Compile(wrapper, Options(), report), report.ToText());
            wrapper.Bind(new LogicBinder(env.Facts, env.Refs, report));
            var dependencies = new List<LogicDependency>(); wrapper.CollectDependencies(dependencies);
            CollectionAssert.AreEquivalent(new[] {
                new LogicDependency(LogicDependencyKind.Item, env.Content.IndexOf(env.Content.Get<ItemDefinition>(new ContentId("resource_fuel")))),
                new LogicDependency(LogicDependencyKind.Fact, env.Facts.SlotOf("intro_awake"))
            }, dependencies);
        }

        [Test] public void UnknownFunction_SuggestsNearest()
        {
            using var env = new LogicTestEnvironment(); var report = new ValidationReport("suggestion");
            Assert.IsFalse(env.Compiler.TryCompileExpression("has_itm(\"resource_fuel\")", Options(), report, out _));
            StringAssert.Contains("did you mean 'has_item'", report.ToText());
        }
    }
}
