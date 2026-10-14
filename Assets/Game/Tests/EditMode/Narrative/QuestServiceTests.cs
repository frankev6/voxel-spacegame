using System;
using System.Collections.Generic;
using System.Reflection;
using Game.App.Services;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Narrative;
using Game.Narrative.Quests;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using static Game.Tests.EditMode.Data.Logic.LogicTestEnvironment;

namespace Game.Tests.EditMode.Narrative
{
    public sealed class QuestServiceTests
    {
        internal sealed class Fixture
        {
            public readonly ContentCatalog Catalog;
            public readonly FactStore Facts;
            public readonly NarrativeService Host;
            public readonly QuestService Quests;
            public readonly LogicContext Context;
            public Fixture(params QuestDefinition[] quests)
            {
                EventHub.Reset(); var definitions = FactCatalogTests.BaseContent(); definitions.AddRange(quests);
                definitions.Add(FactCatalogTests.Facts("facts_quests",
                    new FactDeclaration { Name = "story_count", Kind = FactKind.Int },
                    new FactDeclaration { Name = "story_effects", Kind = FactKind.Int },
                    new FactDeclaration { Name = "story_fail", Kind = FactKind.Bool }));
                Catalog = FactCatalogTests.Content(definitions);
                var refs = RefResolverRegistry.CreateDefault(Catalog); var report = new ValidationReport("quest fixture");
                var factCatalog = FactCatalog.Build(Catalog, refs, report); Facts = new FactStore(factCatalog);
                var compiler = new LogicCompiler(factCatalog, refs, new LogicFunctionRegistry());
                var binder = new LogicBinder(factCatalog, refs, report);
                foreach (var quest in quests) { quest.CompileLogic(compiler, report.For(quest.IdString)); quest.BindLogic(binder); }
                Assert.IsFalse(report.HasErrors, report.ToText());
                var owners = new FakeLogicContext(factCatalog); var adapter = new QuestOwner();
                Context = new LogicContext(Facts, owners, adapter, owners, () => 7);
                Host = new NarrativeService(Facts, Context); Quests = new QuestService(Catalog, Facts, Context, Host);
                adapter.Service = Quests; Host.Register(Quests); Host.BeginNewGame(); Host.SetWorldReady(true); Host.Tick();
            }
            public int Index(QuestDefinition q) => Catalog.IndexOf(q);
            public void Set(string fact, int value) => Facts.SetBySlot(Facts.Catalog.SlotOf(fact), value, FactWriteSource.Debugger);
            public int Get(string fact) => Facts.GetBySlot(Facts.Catalog.SlotOf(fact));
            public void Tick() => Host.Tick();
        }
        sealed class QuestOwner : ILogicNarrative
        {
            public QuestService Service;
            public QuestStatus GetQuestStatus(int q) => Service.GetStatus(q);
            public int GetQuestStage(int q) => Service.GetStage(q);
            public bool IsObjectiveDone(int q, string id) => Service.IsObjectiveDone(q, id);
            public int GetBaseline(in LogicScope scope, int slot, BaselineKind kind) => Service.GetBaseline(scope, slot, kind);
            public bool StartQuest(int q) => Service.Start(q);
            public bool FailQuest(int q) => Service.Fail(q);
            public bool CompleteQuest(int q) => Service.ForceComplete(q);
            public void TrackQuest(int q) => Service.Track(q);
            public void AddQuestLog(int q, string id) => Service.AddLog(q, id);
            public int GetDialogVisitCount(string node) => throw new NotSupportedException();
            public int Roll(int sides) => throw new NotSupportedException();
            public bool RevealClue(int clue) => throw new NotSupportedException();
            public void QueueBeat(int beat) => throw new NotSupportedException();
            public void CompleteBeat(int beat) => throw new NotSupportedException();
            public void AbortCurrentBeat() => throw new NotSupportedException();
        }
        internal static void Field(QuestDefinition q, string name, object value)
            => typeof(QuestDefinition).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(q, value);
        internal static CompiledEffectList Effects(params string[] lines) => Source(new CompiledEffectList(), lines);
        internal static QuestStage Stage(string id, string condition, params string[] effects) => new QuestStage
        {
            id = id, onComplete = Effects(effects), objectives = new List<QuestObjective> {
                new QuestObjective { id = id + "_objective", doneWhen = Source(new CompiledCondition(), condition) } }
        };
        internal static QuestDefinition Quest(string id, params QuestStage[] stages)
        {
            var q = TestContentFactory.Create<QuestDefinition>(id); Field(q, "stages", new List<QuestStage>(stages)); return q;
        }
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        [Test] public void SinceAccept_NotRetroactive()
        {
            var q = Quest("quest_since", Stage("one", "since_accept(\"story_count\") >= 1")); var f = new Fixture(q); int index = f.Index(q);
            f.Set("story_count", 5); Assert.IsTrue(f.Quests.Start(index)); f.Tick();
            Assert.AreEqual(QuestStatus.Active, f.Quests.GetStatus(index));
            f.Set("story_count", 6); f.Tick(); Assert.AreEqual(QuestStatus.Completed, f.Quests.GetStatus(index));
        }
        [Test] public void Stages_ProgressAndBaselines()
        {
            var q = Quest("quest_stages", Stage("one", "$story_count >= 1", "add_fact story_count 2", "add_fact story_effects 1"),
                Stage("two", "$story_count >= 3", "add_fact story_effects 10"),
                Stage("three", "since_stage(\"story_count\") >= 1", "add_fact story_effects 100"));
            Field(q, "onCompleted", Effects("add_fact story_effects 1000")); var f = new Fixture(q); int index = f.Index(q);
            f.Quests.Start(index); f.Set("story_count", 1); f.Tick();
            Assert.AreEqual(2, f.Quests.GetStage(index)); Assert.AreEqual(11, f.Get("story_effects"));
            Assert.AreEqual(3, f.Quests.GetBaseline(new LogicScope(LogicUsage.QuestObjective, index, 0, 0, false, default), f.Facts.Catalog.SlotOf("story_count"), BaselineKind.Stage));
            f.Set("story_count", 4); f.Tick(); Assert.AreEqual(QuestStatus.Completed, f.Quests.GetStatus(index)); Assert.AreEqual(1111, f.Get("story_effects"));
            Assert.AreEqual(LogicUsage.None, f.Context.Scope.Usage);
        }
        [Test] public void Restore_NoEffects()
        {
            var q = Quest("quest_restore", Stage("one", "$story_count >= 1", "add_fact story_effects 1"));
            Field(q, "failWhen", Source(new CompiledCondition(), "$story_fail")); Field(q, "onFailed", Effects("add_fact story_effects 100"));
            var f = new Fixture(q); int index = f.Index(q); f.Quests.Start(index); f.Tick();
            f.Set("story_count", 1); f.Set("story_fail", 1); f.Host.OnSessionRestored(); f.Tick();
            Assert.AreEqual(QuestStatus.Active, f.Quests.GetStatus(index)); Assert.AreEqual(0, f.Quests.GetStage(index));
            Assert.IsFalse(f.Quests.IsObjectiveDone(index, "one_objective")); Assert.AreEqual(0, f.Get("story_effects"));
            f.Set("story_fail", 0); f.Tick(); Assert.AreEqual(QuestStatus.Completed, f.Quests.GetStatus(index)); Assert.AreEqual(1, f.Get("story_effects"));
        }
        [Test] public void Start_Idempotent()
        {
            var q = Quest("quest_once", Stage("one", "$story_count > 100")); Field(q, "onAccepted", Effects("add_fact story_effects 1"));
            var f = new Fixture(q); int index = f.Index(q);
            Assert.IsTrue(f.Quests.Start(index)); Assert.IsFalse(f.Quests.Start(index)); Assert.AreEqual(1, f.Get("story_effects"));
            Assert.IsTrue(f.Quests.ForceComplete(index)); Assert.IsFalse(f.Quests.Start(index)); Assert.AreEqual(1, f.Quests.GetInstanceCount(index));
        }
        [Test] public void Contract_Instances()
        {
            var q = Quest("contract_repeat", Stage("one", "since_accept(\"story_count\") > 0")); Field(q, "kind", QuestKind.Contract);
            var f = new Fixture(q); int index = f.Index(q); f.Set("story_count", 5);
            f.Quests.Start(index); f.Quests.ForceComplete(index); f.Set("story_count", 8); Assert.IsTrue(f.Quests.Start(index));
            Assert.AreEqual(2, f.Quests.GetInstanceCount(index));
            int slot = f.Facts.Catalog.SlotOf("story_count");
            Assert.AreEqual(5, f.Quests.GetBaseline(new LogicScope(LogicUsage.QuestObjective, index, 0, 0, false, default), slot, BaselineKind.Accept));
            Assert.AreEqual(8, f.Quests.GetBaseline(new LogicScope(LogicUsage.QuestObjective, index, 1, 0, false, default), slot, BaselineKind.Accept));
            f.Tick(); Assert.AreEqual(QuestStatus.Active, f.Quests.GetStatus(index));
        }
        [Test] public void FailWhen_Once()
        {
            var q = Quest("quest_fail", Stage("one", "$story_count > 100")); Field(q, "failWhen", Source(new CompiledCondition(), "$story_fail"));
            Field(q, "onFailed", Effects("add_fact story_effects 1")); var f = new Fixture(q); int index = f.Index(q);
            f.Quests.Start(index); f.Set("story_fail", 1); f.Tick(); f.Set("story_count", 2); f.Tick();
            Assert.AreEqual(QuestStatus.Failed, f.Quests.GetStatus(index)); Assert.AreEqual(1, f.Get("story_effects")); Assert.IsFalse(f.Quests.Fail(index));
        }
        [Test] public void Track_RetracksOnComplete()
        {
            var a = Quest("quest_a", Stage("one", "$story_count > 100")); var b = Quest("quest_b", Stage("one", "$story_count > 100"));
            var f = new Fixture(a, b); int ai = f.Index(a), bi = f.Index(b); f.Quests.Start(ai); f.Quests.Start(bi);
            Assert.AreEqual(bi, f.Quests.TrackedQuestIndex); f.Quests.ForceComplete(bi); Assert.AreEqual(ai, f.Quests.TrackedQuestIndex);
            f.Quests.Fail(ai); Assert.AreEqual(0, f.Quests.TrackedQuestIndex);
        }
    }
}
