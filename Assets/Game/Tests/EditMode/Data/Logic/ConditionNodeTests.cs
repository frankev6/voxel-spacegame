using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Logic.Nodes;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Tests.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data.Logic
{
    [Category("Narrative")]
    public sealed class ConditionNodeTests : GameTestBase
    {
        [Test] public void EachFunction_TrueAndFalse()
        {
            using var f = new Fixture();
            void Case(string expression, Action<bool> arrange, LogicUsage usage = LogicUsage.BeatCondition)
            {
                var node = f.Condition(expression, usage); f.Context.DefaultScope = Scope(usage);
                foreach (bool value in new[] { true, false }) { arrange(value); Assert.AreEqual(value, node.Evaluate(f.Context), expression); }
                var dependencies = new List<LogicDependency>(); node.CollectDependencies(dependencies);
                var again = new List<LogicDependency>(); node.CollectDependencies(again); CollectionAssert.AreEqual(dependencies, again);
            }
            int count = f.Facts.SlotOf("story_count"), awake = f.Facts.SlotOf("intro_awake");
            Case("$intro_awake", on => f.Context.Values[awake] = on ? 1 : 0);
            Case("$story_count >= 3", on => f.Context.Values[count] = on ? 3 : 2);
            Case("has_item(\"resource_fuel\",2)", on => f.Context.Items[f.Item] = on ? 2 : 1);
            Case("has_item(\"resource_fuel\")", on => f.Context.Items[f.Item] = on ? 1 : 0);
            Case("item_count(\"resource_fuel\") >= 2", on => f.Context.Items[f.Item] = on ? 2 : 1);
            Case("credits() >= 10", on => f.Context.Credits = on ? 10 : 9);
            foreach (var row in new[] { ("quest_not_started", QuestStatus.NotStarted), ("quest_active", QuestStatus.Active),
                ("quest_completed", QuestStatus.Completed), ("quest_failed", QuestStatus.Failed) })
                Case(row.Item1 + "(\"quest_sample\")", on => f.Context.Status = on ? row.Item2 : QuestStatus.Abandoned);
            Case("quest_stage(\"quest_sample\") == 2", on => { f.Context.Stage = 2; f.Context.Status = on ? QuestStatus.Active : QuestStatus.Completed; });
            Case("objective_done(\"quest_sample\",\"core\")", on => f.Context.ObjectiveDone = on);
            Case("clue_known(\"clue_sample\")", on => f.Context.Values[f.Facts.SlotOf("clue_sample")] = on ? 2 : 0);
            Case("mystery_solved(\"mystery_sample\")", on => f.Context.Values[f.Facts.SlotOf("mystery_sample")] = on ? 1 : 2);
            Case("beat_count(\"beat_sample\") >= 3", on => f.Context.Values[f.Facts.SlotOf("beat_sample")] = on ? 3 : 2);
            f.Context.Baselines[(1, 0, count, BaselineKind.Accept)] = 5;
            Case("since_accept(\"story_count\") >= 3", on => f.Context.Values[count] = on ? 8 : 7, LogicUsage.QuestObjective);
            f.Context.Baselines[(1, 0, count, BaselineKind.Stage)] = 10;
            Case("since_stage(\"story_count\") >= 3", on => f.Context.Values[count] = on ? 13 : 12, LogicUsage.QuestObjective);
            Case("in_star_system(\"starsys_sample\")", on => f.Context.CurrentStarSystemIndex = on ? f.System : 0);
            Case("relation(\"faction_sample\") >= 20", on => f.Context.Relations[f.Faction] = on ? 20 : 19);
            Case("relation(\"owner\") >= 20", on => { f.Context.Relations[f.Faction] = 20; f.Context.CurrentStarSystemOwnerFactionIndex = on ? f.Faction : 0; });
            var ownerDependencies = new List<LogicDependency>(); f.Condition("relation(\"owner\") >= 0").CollectDependencies(ownerDependencies);
            CollectionAssert.Contains(ownerDependencies, new LogicDependency(LogicDependencyKind.Location, 0));
            CollectionAssert.Contains(ownerDependencies, new LogicDependency(LogicDependencyKind.Relation, f.Faction));
            Case("star_system_owned()", on => f.Context.CurrentStarSystemOwnerFactionIndex = on ? f.Faction : 0);
            Case("game_hours() >= 2", on => f.Context.UniversalTime = on ? 7200 : 7199);
            Case("roll(6) == 3", on => f.Context.RollValue = on ? 3 : 2, LogicUsage.DialogCondition);
            Assert.AreEqual(6, f.Context.LastRollSides);
            Case("visited_count(\"node_sample\") >= 2", on => f.Context.Visits = on ? 2 : 1, LogicUsage.DialogCondition);
            Assert.AreEqual(0, f.Context.CatalogReads, "Bound generated reads must not repeat catalog lookup.");
            var report = new ValidationReport("bad node"); new NotCondition().Validate(report); Assert.Greater(report.ErrorCount, 0);
            Assert.IsFalse(new FactBoolRead("intro_awake").Evaluate(f.Context)); Assert.AreEqual(0, new FactIntRead("story_count").Evaluate(f.Context));
            Assert.IsTrue(f.Condition("").Evaluate(f.Context)); Assert.AreEqual(0, f.Integer("").Evaluate(f.Context));
        }

        [Test] public void ShortCircuit()
        {
            var left = new CountingCondition { Value = true }; var right = new CountingCondition { Value = false };
            Assert.IsTrue(new OrCondition(left, right).Evaluate(null)); Assert.AreEqual(1, left.Calls); Assert.AreEqual(0, right.Calls);
            left.Value = false; Assert.IsFalse(new OrCondition(left, right).Evaluate(null)); Assert.AreEqual(2, left.Calls); Assert.AreEqual(1, right.Calls);
            left.Calls = right.Calls = 0; Assert.IsFalse(new AndCondition(left, right).Evaluate(null)); Assert.AreEqual(1, left.Calls); Assert.AreEqual(0, right.Calls);
            left.Value = true; right.Value = true; Assert.IsTrue(new AndCondition(left, right).Evaluate(null)); Assert.AreEqual(2, left.Calls); Assert.AreEqual(1, right.Calls);
            using var f = new Fixture(); f.Context.DefaultScope = Scope(LogicUsage.DialogCondition);
            Assert.IsTrue(f.Condition("true or roll(6) == 1", LogicUsage.DialogCondition).Evaluate(f.Context)); Assert.AreEqual(0, f.Context.RollCalls);
            Assert.IsFalse(f.Condition("false and roll(6) == 1", LogicUsage.DialogCondition).Evaluate(f.Context)); Assert.AreEqual(0, f.Context.RollCalls);
            Assert.IsFalse(new NotCondition().Evaluate(null));
            const string source = "nar004_unavailable()";
            var failed = LogicTestEnvironment.Source(new CompiledCondition(), source);
            Assert.IsFalse(f.Compiler.Compile(failed, new LogicCompileOptions(LogicUsage.BeatCondition, "test:invalid", "condition"), new ValidationReport("invalid")));
            string key = "NAR.Condition." + StableHash.Hash64(source); int previous = GameLog.GetOnceCount(key);
            if (previous == 0) LogAssert.Expect(LogType.Warning, new Regex("Condition failed to compile; evaluates false"));
            Assert.IsFalse(failed.Evaluate(f.Context)); Assert.IsFalse(failed.Evaluate(f.Context));
            Assert.AreEqual(previous + 1, GameLog.GetOnceCount(key), "A failed wrapper reports only on its first evaluation.");
        }

        [Test] public void SaturatingArithmetic()
        {
            using var f = new Fixture();
            foreach (var row in new[] { ("2147483647 + 1", int.MaxValue), ("-2147483647 - 2", int.MinValue),
                ("-(-2147483647 - 1)", int.MaxValue), ("2147483647 - (-1)", int.MaxValue),
                ("(-2147483647 - 1) + (-1)", int.MinValue), ("(2147483647 + 1) - 1", int.MaxValue - 1), ("8 - 3 + -2", 3) })
                Assert.AreEqual(row.Item2, f.Integer(row.Item1).Evaluate(f.Context), row.Item1);
            Assert.AreEqual(int.MaxValue, new NegateExpr(new IntLiteral(int.MinValue)).Evaluate(null));
            f.Context.Credits = long.MaxValue; Assert.AreEqual(int.MaxValue, f.Integer("credits()").Evaluate(f.Context));
            f.Context.Credits = -1; Assert.AreEqual(0, f.Integer("credits()").Evaluate(f.Context));
            f.Context.UniversalTime = double.MaxValue; Assert.AreEqual(int.MaxValue, f.Integer("game_hours()").Evaluate(f.Context));
            f.Context.UniversalTime = -double.MaxValue; Assert.AreEqual(int.MinValue, f.Integer("game_hours()").Evaluate(f.Context));
            foreach (var row in new[] { (CompareOp.Equal, false), (CompareOp.NotEqual, true), (CompareOp.Less, true),
                (CompareOp.LessEqual, true), (CompareOp.Greater, false), (CompareOp.GreaterEqual, false) })
                Assert.AreEqual(row.Item2, new CompareIntCondition(row.Item1, new IntLiteral(int.MinValue), new IntLiteral(int.MaxValue)).Evaluate(null));
            Assert.IsTrue(new CompareBoolCondition(CompareOp.NotEqual, new BoolLiteral(true), new BoolLiteral(false)).Evaluate(null));
            Assert.IsTrue(new CompareBoolCondition(CompareOp.Equal, new BoolLiteral(true), new BoolLiteral(true)).Evaluate(null));
        }

        [Test] public void SinceAccept_UsesScopeBaseline()
        {
            using var f = new Fixture(); int slot = f.Facts.SlotOf("story_count"); f.Context.Values[slot] = 10;
            var accept = f.Integer("since_accept(\"story_count\")", LogicUsage.QuestObjective);
            var stage = f.Integer("since_stage(\"story_count\")", LogicUsage.QuestObjective);
            f.Context.DefaultScope = Scope(LogicUsage.QuestObjective);
            f.Context.Baselines[(1, 0, slot, BaselineKind.Accept)] = 9; Assert.AreEqual(1, accept.Evaluate(f.Context));
            var nested = new LogicScope(LogicUsage.QuestObjective, 2, 3, 4, false, default);
            f.Context.Baselines[(2, 3, slot, BaselineKind.Accept)] = 4; f.Context.Baselines[(2, 3, slot, BaselineKind.Stage)] = 13;
            f.Context.PushScope(nested); Assert.AreEqual(6, accept.Evaluate(f.Context)); Assert.AreEqual(-3, stage.Evaluate(f.Context));
            Assert.AreEqual(2, f.Context.LastBaselineScope.QuestIndex); Assert.AreEqual(3, f.Context.LastBaselineScope.QuestInstance);
            Assert.AreEqual(slot, f.Context.LastBaselineSlot); Assert.AreEqual(BaselineKind.Stage, f.Context.LastBaselineKind);
            f.Context.PopScope(); Assert.AreEqual(1, accept.Evaluate(f.Context));
            f.Context.Values[slot] = int.MaxValue; f.Context.Baselines[(1, 0, slot, BaselineKind.Accept)] = int.MinValue;
            Assert.AreEqual(int.MaxValue, accept.Evaluate(f.Context));
            f.Context.Values[slot] = int.MinValue; f.Context.Baselines[(1, 0, slot, BaselineKind.Stage)] = int.MaxValue;
            Assert.AreEqual(int.MinValue, stage.Evaluate(f.Context));
        }

        [Test] public void Evaluate_DoesNotAllocate()
        {
            using var f = new Fixture();
            // Exactly ten emitted nodes, including one optional inventory call, typed arithmetic, comparison and short circuit.
            var condition = f.Condition("has_item(\"resource_fuel\",2) and ($story_count + 1 >= credits() or $intro_awake)");
            int count = f.Facts.SlotOf("story_count"), awake = f.Facts.SlotOf("intro_awake");
            f.Context.Values[count] = 5; f.Context.Values[awake] = 1; f.Context.Items[f.Item] = 2; f.Context.Credits = 6;
            var extras = new[] { f.Condition("clue_known(\"clue_sample\")"), f.Condition("mystery_solved(\"mystery_sample\")") };
            var integer = f.Integer("beat_count(\"beat_sample\") + item_count(\"resource_fuel\") + relation(\"owner\")");
            for (int i = 0; i < 64; i++) { condition.Evaluate(f.Context); foreach (var extra in extras) extra.Evaluate(f.Context); integer.Evaluate(f.Context); }
            int trueCount = 0, checksum = 0; long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                f.Context.Credits = i % 2 == 0 ? 6 : 20;
                if (condition.Evaluate(f.Context)) trueCount++;
                foreach (var extra in extras) if (extra.Evaluate(f.Context)) checksum++;
                checksum += integer.Evaluate(f.Context);
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(1000, trueCount); Assert.AreEqual(2000, checksum); Assert.AreEqual(0, bytes);
            Assert.AreEqual(0, f.Context.CatalogReads);
        }

        static LogicScope Scope(LogicUsage usage) => new LogicScope(usage, 1, 0, 0, false, default);
        sealed class CountingCondition : ICondition
        {
            public bool Value; public int Calls;
            public bool Evaluate(ILogicContext context) { Calls++; return Value; }
            public void Validate(ValidationReport report) { }
            public void Bind(LogicBinder binder) { }
            public void CollectDependencies(List<LogicDependency> into) { }
        }
        sealed class Fixture : IDisposable
        {
            public readonly ContentCatalog Content;
            public readonly FactCatalog Facts;
            public readonly RefResolverRegistry Refs;
            public readonly LogicCompiler Compiler;
            public readonly FakeLogicContext Context;
            public readonly int Item, System, Faction;
            public Fixture()
            {
                var definitions = FactCatalogTests.BaseContent(); definitions.Add(FuelDefinition());
                definitions.Add(Create<QuestDefinition>("quest_sample")); definitions.Add(Create<ClueDefinition>("clue_sample"));
                definitions.Add(Create<MysteryDefinition>("mystery_sample")); definitions.Add(Create<StoryBeatDefinition>("beat_sample"));
                definitions.Add(Create<AuthoredStarSystemDefinition>("starsys_sample")); definitions.Add(Create<FactionDefinition>("faction_sample"));
                definitions.Add(FactCatalogTests.Facts("facts_conditions", new FactDeclaration { Name = "intro_awake", Kind = FactKind.Bool },
                    new FactDeclaration { Name = "story_count", Kind = FactKind.Int }));
                Content = FactCatalogTests.Content(definitions); Refs = RefResolverRegistry.CreateDefault(Content);
                var report = new ValidationReport("condition fixture"); Facts = FactCatalog.Build(Content, Refs, report);
                Assert.AreEqual(0, report.ErrorCount, report.ToText()); Compiler = new LogicCompiler(Facts, Refs, new LogicFunctionRegistry());
                Context = new FakeLogicContext(Facts);
                Item = Content.IndexOf(Content.Get<ItemDefinition>(new ContentId("resource_fuel")));
                System = Content.IndexOf(Content.Get<AuthoredStarSystemDefinition>(new ContentId("starsys_sample")));
                Faction = Content.IndexOf(Content.Get<FactionDefinition>(new ContentId("faction_sample")));
            }
            static ItemDefinition FuelDefinition() => TestContentFactory.Item("resource_fuel");
            public CompiledCondition Condition(string source, LogicUsage usage = LogicUsage.BeatCondition)
            {
                var node = LogicTestEnvironment.Source(new CompiledCondition(), source); var report = new ValidationReport(source);
                Assert.IsTrue(Compiler.Compile(node, new LogicCompileOptions(usage, "test:condition", "condition"), report), report.ToText());
                node.Validate(report); node.Bind(new LogicBinder(Facts, Refs, report)); Assert.AreEqual(0, report.ErrorCount, report.ToText()); return node;
            }
            public CompiledIntExpr Integer(string source, LogicUsage usage = LogicUsage.BeatCondition)
            {
                var node = LogicTestEnvironment.Source(new CompiledIntExpr(), source); var report = new ValidationReport(source);
                Assert.IsTrue(Compiler.Compile(node, new LogicCompileOptions(usage, "test:condition", "progress"), report), report.ToText());
                node.Validate(report); node.Bind(new LogicBinder(Facts, Refs, report)); Assert.AreEqual(0, report.ErrorCount, report.ToText()); return node;
            }
            public void Dispose() => DestroyAll();
        }
    }
}
