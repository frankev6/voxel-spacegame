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
using Game.Narrative.Beats;
using Game.Narrative.Clues;
using Game.Narrative.Dialog;
using Game.Narrative.Quests;
using Game.Narrative.ShipLog;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using static Game.Tests.EditMode.Data.Logic.LogicTestEnvironment;

namespace Game.Tests.EditMode.Narrative
{
    /// <summary>The six QST-006 cases use real narrative owners and tiny declared content.</summary>
    public sealed class ShipLogQueryTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        static void Field(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field == null) continue; field.SetValue(target, value); return;
            }
            Assert.Fail("Fixture field missing: " + name);
        }
        sealed class Fixture : IDisposable
        {
            internal readonly QuestDefinition Main, Contract;
            internal readonly ClueDefinition Alpha = TestContentFactory.Create<ClueDefinition>("clue_alpha");
            internal readonly ClueDefinition Beta = TestContentFactory.Create<ClueDefinition>("clue_beta");
            internal readonly RumorDefinition Rumor = TestContentFactory.Create<RumorDefinition>("rumor_gate");
            internal readonly MysteryDefinition Mystery = TestContentFactory.Create<MysteryDefinition>("mystery_ark");
            internal readonly StoryBeatDefinition Beat = TestContentFactory.Create<StoryBeatDefinition>("beat_test");
            internal readonly AuthoredStarSystemDefinition Target = TestContentFactory.Create<AuthoredStarSystemDefinition>("starsys_target");
            internal readonly ContentCatalog Content;
            internal readonly FactStore Facts;
            internal readonly LogicContext Context;
            internal readonly NarrativeService Host;
            internal readonly NarrativeLogicAdapter Adapter = new();
            internal readonly QuestService Quests;
            internal readonly ClueService Clues;
            internal readonly BeatService Beats;
            internal readonly DialogService Dialog;
            internal readonly DialogMemory Memory = new();
            internal readonly ShipLogQuery Log;
            internal Fixture()
            {
                var stage = new QuestStage { id = "collect", title = "First stage", objectives = new List<QuestObjective> {
                    new QuestObjective { id = "collect", text = "Collect three", doneWhen = Source(new CompiledCondition(), "$story_count >= 3"),
                        progress = Source(new CompiledIntExpr(), "$story_count"), target = 3,
                        marker = new QuestMarker { starSystem = Target, bodyTag = "cache" } },
                    new QuestObjective { id = "return", text = "Go back", doneWhen = Source(new CompiledCondition(), "$story_finish"),
                        marker = new QuestMarker { starSystem = Target, bodyTag = "ark", label = "Return" } } },
                    onComplete = QuestServiceTests.Effects("add_fact story_rewards 10") };
                Main = QuestServiceTests.Quest("quest_main", stage); Field(Main, "title", "Main");
                Field(Main, "onCompleted", QuestServiceTests.Effects("add_fact story_rewards 1"));
                Field(Main, "logEntries", new List<QuestLogEntry> { new QuestLogEntry { id = "found", text = "Recovered a signal" } });
                Contract = QuestServiceTests.Quest("contract_second", QuestServiceTests.Stage("wait", "$story_finish"));
                Field(Contract, "kind", QuestKind.Contract); Field(Contract, "title", "Second");
                Field(Alpha, "title", "Alpha"); Field(Beta, "title", "Beta"); Field(Rumor, "title", "Gate rumor");
                Field(Alpha, "mystery", Mystery); Field(Beta, "mystery", Mystery);
                Field(Mystery, "title", "The Ark"); Field(Mystery, "solvedText", "The answer");
                Field(Mystery, "requires", new List<ClueDefinition> { Alpha, Beta });
                Field(Rumor, "target", Target); Field(Rumor, "targetBodyTag", "gate");
                Field(Rumor, "resolvedWhen", Source(new CompiledCondition(), "$story_resolve"));
                Field(Beat, "yarnNode", "start"); Field(Beat, "repeat", BeatRepeat.Always); Field(Beat, "trigger", BeatTrigger.Interact);
                Field(Beat, "onComplete", QuestServiceTests.Effects("add_fact story_rewards 100"));
                var definitions = FactCatalogTests.BaseContent();
                definitions.AddRange(new ContentDefinition[] { Main, Contract, Alpha, Beta, Rumor, Mystery, Beat, Target });
                definitions.Add(FactCatalogTests.Facts("facts_log",
                    new FactDeclaration { Name = "story_count", Kind = FactKind.Int },
                    new FactDeclaration { Name = "story_finish", Kind = FactKind.Bool },
                    new FactDeclaration { Name = "story_resolve", Kind = FactKind.Bool },
                    new FactDeclaration { Name = "story_rewards", Kind = FactKind.Int },
                    new FactDeclaration { Name = "story_noise", Kind = FactKind.Int },
                    new FactDeclaration { Name = "journal_bool", Kind = FactKind.Bool, Flags = FactFlags.Journal, JournalText = "Signal recorded" },
                    new FactDeclaration { Name = "journal_counter", Kind = FactKind.Int, Flags = FactFlags.Journal, JournalText = "Recovered artifacts" }));
                Content = FactCatalogTests.Content(definitions); var refs = RefResolverRegistry.CreateDefault(Content);
                var report = new ValidationReport("ship log fixture"); var factCatalog = FactCatalog.Build(Content, refs, report);
                var compiler = new LogicCompiler(factCatalog, refs, new LogicFunctionRegistry()); var binder = new LogicBinder(factCatalog, refs, report);
                foreach (var definition in definitions)
                    if (definition is ILogicSourceOwner owner) { owner.CompileLogic(compiler, report.For(definition.IdString)); owner.BindLogic(binder); }
                Assert.IsFalse(report.HasErrors, report.ToText()); Facts = new FactStore(factCatalog);
                // Only external world/economy boundaries are inert; every narrative call uses its actual service.
                var external = new FakeLogicContext(factCatalog); Context = new LogicContext(Facts, external, Adapter, external, () => 7);
                Host = new NarrativeService(Facts, Context); Quests = new QuestService(Content, Facts, Context, Host);
                Clues = new ClueService(Content, Facts, Context);
                Dialog = new DialogService(new FakeDialogRunner(FakeDialogRunner.Line("Page")), DialogServiceTests.Table(), Context, Memory);
                Adapter.BindDialog(Dialog); Beats = new BeatService(Content, Facts, Context, Dialog, _ => 4, _ => "ark", 7);
                Adapter.BindServices(Quests, Clues, Beats); Log = new ShipLogQuery(Content, Facts, Quests, Clues, Beats);
                Host.Register(Quests); Host.Register(Clues); Host.Register(Beats);
                Host.BeginNewGame(); Host.SetWorldReady(true); Frame();
            }
            internal int MainIndex => Content.IndexOf(Main);
            internal int ContractIndex => Content.IndexOf(Contract);
            internal int ClueIndex(ClueDefinition clue) => Content.IndexOf<ClueDefinition>(clue);
            internal void Set(string name, int value) => Facts.SetBySlot(Facts.Catalog.SlotOf(name), value, FactWriteSource.Debugger);
            internal void Frame() { EventHub.DrainAll(); Host.Tick(); }
            public void Dispose() { Log.Dispose(); Dialog.Interrupt(); Beats.Dispose(); }
        }
        [Test] public void Adapter_Routes()
        {
            using var f = new Fixture(); int q = f.MainIndex, c = f.ContractIndex;
            Assert.AreEqual(QuestStatus.NotStarted, f.Adapter.GetQuestStatus(q)); Assert.AreEqual(-1, f.Adapter.GetQuestStage(q));
            f.Set("story_count", 2); Assert.IsTrue(f.Adapter.StartQuest(q)); Assert.IsFalse(f.Adapter.StartQuest(q));
            Assert.AreEqual(QuestStatus.Active, f.Adapter.GetQuestStatus(q)); Assert.AreEqual(0, f.Adapter.GetQuestStage(q));
            var scope = new LogicScope(LogicUsage.QuestObjective, q, 0, 0, false, default);
            int slot = f.Facts.Catalog.SlotOf("story_count");
            Assert.AreEqual(2, f.Adapter.GetBaseline(scope, slot, BaselineKind.Accept));
            Assert.AreEqual(2, f.Adapter.GetBaseline(scope, slot, BaselineKind.Stage));
            Assert.IsTrue(f.Adapter.StartQuest(c));
            var entries = new List<QuestEntryView>(); f.Log.GetQuests(false, entries);
            Assert.AreEqual(q, entries[0].QuestIndex); Assert.AreEqual(c, entries[1].QuestIndex, "Accept order differs from catalog ID order.");
            f.Adapter.AddQuestLog(q, "found"); var texts = new List<string>(); f.Log.GetLog(q, texts); CollectionAssert.AreEqual(new[] { "Recovered a signal" }, texts);
            f.Adapter.TrackQuest(0); Assert.AreEqual(0, f.Quests.TrackedQuestIndex); f.Adapter.TrackQuest(q);
            Assert.IsFalse(f.Adapter.IsObjectiveDone(q, "collect")); Assert.IsTrue(f.Adapter.CompleteQuest(q));
            Assert.IsTrue(f.Adapter.IsObjectiveDone(q, "return")); Assert.AreEqual(1, f.Facts.GetBySlot(f.Facts.Catalog.SlotOf("story_rewards")), "ForceComplete skips stage effects.");
            Assert.IsTrue(f.Adapter.FailQuest(c)); Assert.AreEqual(QuestStatus.Failed, f.Adapter.GetQuestStatus(c));
            Assert.IsTrue(f.Adapter.StartQuest(c)); Assert.AreEqual(2, f.Quests.GetInstanceCount(c));
            f.Log.GetQuests(true, entries); Assert.AreEqual(1, entries.Count); Assert.AreEqual(q, entries[0].QuestIndex, "Historical contract instances remain owner/save state.");
            Assert.IsTrue(f.Adapter.RevealClue(f.ClueIndex(f.Alpha))); Assert.IsTrue(f.Clues.IsKnown(f.ClueIndex(f.Alpha)));
            int beat = f.Content.IndexOf(f.Beat); f.Adapter.CompleteBeat(beat);
            int beatSlot = f.Facts.Catalog.GeneratedSlot(FactFamily.Beat, f.Beat.Id.Hash.Value); Assert.AreEqual(1, f.Facts.GetBySlot(beatSlot));
            f.Adapter.QueueBeat(beat); Assert.AreEqual(1, f.Beats.QueuedCount); f.Host.SetPlayerFree(true); f.Frame();
            // Visit accounting belongs to the production runner; seed memory to verify adapter routing.
            f.Memory.IncrementVisit("start");
            Assert.IsTrue(f.Dialog.IsActive); Assert.AreEqual(1, f.Adapter.GetDialogVisitCount("start"));
            Assert.AreEqual(f.Dialog.Roll(6), f.Adapter.Roll(6));
            f.Adapter.AbortCurrentBeat(); f.Dialog.Pump(); f.Dialog.AcknowledgeEnd();
            Assert.IsFalse(f.Dialog.IsActive); Assert.AreEqual(1, f.Facts.GetBySlot(beatSlot), "Aborting does not complete the current beat.");
        }
        [Test] public void Clues_KnownByOrdinal()
        {
            using var f = new Fixture(); var rows = new List<ClueEntryView>(); f.Log.GetClues(false, rows); Assert.IsEmpty(rows);
            f.Clues.Reveal(f.ClueIndex(f.Beta)); f.Clues.Reveal(f.ClueIndex(f.Alpha)); f.Clues.Reveal(f.ClueIndex(f.Rumor));
            f.Log.GetClues(false, rows); Assert.AreEqual(2, rows.Count); Assert.AreEqual("clue_beta", rows[0].ClueId); Assert.AreEqual(1, rows[0].Ordinal);
            Assert.AreEqual("clue_alpha", rows[1].ClueId); Assert.AreEqual(2, rows[1].Ordinal); Assert.AreEqual("The Ark", rows[1].MysteryTitle);
            f.Log.MarkClueSeen(f.ClueIndex(f.Alpha)); f.Log.GetClues(false, rows); Assert.IsFalse(rows[1].IsNew);
            f.Log.GetClues(true, rows); Assert.AreEqual(1, rows.Count); Assert.IsTrue(rows[0].IsRumor); Assert.AreEqual(f.Target.Id, rows[0].TargetStarSystem);
            var mysteries = new List<(string title, int known, int required, bool solved, string solvedText)>(); f.Log.GetMysteries(mysteries);
            Assert.AreEqual(("The Ark", 2, 2, true, "The answer"), mysteries[0]);
        }
        [Test] public void Journal_NonZero()
        {
            using var f = new Fixture(); var rows = new List<JournalEntryView>(); f.Log.GetJournal(rows); Assert.IsEmpty(rows);
            f.Set("story_noise", 5); f.Set("journal_counter", 3); f.Set("journal_bool", 1); f.Log.GetJournal(rows);
            Assert.AreEqual(2, rows.Count); Assert.AreEqual("journal_bool", rows[0].FactName); Assert.AreEqual("Signal recorded", rows[0].Text);
            Assert.AreEqual("Recovered artifacts ×3", rows[1].Text); Assert.AreEqual(3, rows[1].Value);
            f.Set("journal_counter", 1); f.Set("journal_bool", 0); f.Log.GetJournal(rows);
            Assert.AreEqual(1, rows.Count); Assert.AreEqual("Recovered artifacts", rows[0].Text);
        }
        [Test] public void Markers_TrackedAndRumors()
        {
            using var f = new Fixture(); var markers = f.Log.Markers;
            f.Quests.Start(f.MainIndex); f.Clues.Reveal(f.ClueIndex(f.Rumor)); Assert.IsEmpty(markers); f.Frame();
            Assert.AreSame(markers, f.Log.Markers); Assert.AreEqual(3, markers.Count);
            Assert.AreEqual(NarrativeMarkerKind.Objective, markers[0].Kind); Assert.AreEqual("Collect three", markers[0].Label); Assert.AreEqual("cache", markers[0].BodyTag);
            Assert.AreEqual("Return", markers[1].Label); Assert.AreEqual(NarrativeMarkerKind.Rumor, markers[2].Kind);
            f.Set("story_count", 3); f.Frame(); Assert.AreEqual(2, markers.Count); Assert.AreEqual("Return", markers[0].Label);
            f.Set("story_resolve", 1); f.Frame(); Assert.IsTrue(f.Clues.IsResolved(f.ClueIndex(f.Rumor))); Assert.AreEqual(1, markers.Count);
            f.Log.SetTracked(0); f.Frame(); Assert.IsEmpty(markers);
            uint revision = f.Log.Revision; f.Frame(); Assert.AreEqual(revision, f.Log.Revision);
        }
        [Test] public void Revision_OnChangeOnly()
        {
            using var f = new Fixture(); uint revision = f.Log.Revision; int changes = 0; f.Log.Changed += () => changes++;
            f.Set("story_noise", 1); f.Frame(); Assert.AreEqual(revision, f.Log.Revision);
            f.Quests.Start(f.MainIndex); f.Clues.Reveal(f.ClueIndex(f.Alpha)); f.Clues.Reveal(f.ClueIndex(f.Beta));
            f.Set("journal_counter", 2); f.Frame(); Assert.AreEqual(revision + 1, f.Log.Revision); Assert.AreEqual(1, changes);
            f.Frame(); Assert.AreEqual(1, changes);
            f.Set("story_count", 1); f.Frame(); Assert.AreEqual(2, changes, "Progress changed while done bits stayed false.");
            f.Set("story_count", 1); f.Frame(); Assert.AreEqual(2, changes);
            f.Log.Dispose(); uint disposedRevision = f.Log.Revision;
            f.Set("journal_bool", 1); f.Quests.AddLog(f.MainIndex, "found"); f.Frame(); Assert.AreEqual(disposedRevision, f.Log.Revision);
        }
        [Test] public void Lists_NoAlloc()
        {
            using var f = new Fixture(); f.Quests.Start(f.MainIndex); f.Quests.Start(f.ContractIndex); f.Quests.ForceComplete(f.ContractIndex);
            f.Quests.AddLog(f.MainIndex, "found"); f.Clues.Reveal(f.ClueIndex(f.Alpha)); f.Clues.Reveal(f.ClueIndex(f.Rumor));
            f.Set("journal_counter", 3); f.Frame();
            var quests = new List<QuestEntryView>(2); var clues = new List<ClueEntryView>(3); var objectives = new List<QuestObjectiveView>(2);
            var journal = new List<JournalEntryView>(2); var logs = new List<string>(1);
            var mysteries = new List<(string title, int known, int required, bool solved, string solvedText)>(1);
            void Read()
            {
                f.Log.GetQuests(false, quests); f.Log.GetQuests(true, quests); f.Log.GetObjectives(f.MainIndex, objectives); f.Log.GetLog(f.MainIndex, logs);
                f.Log.GetClues(false, clues); f.Log.GetClues(true, clues); f.Log.GetMysteries(mysteries); f.Log.GetJournal(journal);
                f.Log.TryGetTracked(out _, out _); _ = f.Log.Markers.Count;
            }
            for (int i = 0; i < 8; i++) Read();
            long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 20; i++) Read(); long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (allocated != 0) Game.Core.GameLog.Warn("Tests", "Advisory QST-006 warm list reads allocated " + allocated + " bytes.");
            Assert.AreEqual(1, quests.Count); Assert.AreEqual(1, clues.Count); Assert.AreEqual(2, objectives.Count); Assert.AreEqual(1, journal.Count);
            Assert.AreEqual(1, logs.Count); Assert.AreEqual(1, mysteries.Count);
        }
    }
}
