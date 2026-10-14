using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.App.Services;
using Game.Core;
using Game.Core.Events;
using Game.Core.Save;
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
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Data.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using static Game.Tests.EditMode.Data.Logic.LogicTestEnvironment;

namespace Game.Tests.EditMode.Narrative
{
    public sealed class QuestSaveTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        internal sealed class Fixture : IDisposable
        {
            internal readonly QuestDefinition Story, Contract;
            internal readonly StoryBeatDefinition Beat;
            internal readonly ClueDefinition Clue;
            internal readonly RumorDefinition Rumor;
            internal readonly ContentCatalog Catalog;
            internal readonly FactStore Facts;
            internal readonly QuestService Quests;
            internal readonly ClueService Clues;
            internal readonly BeatService Beats;
            internal readonly NarrativeService Host;
            internal readonly DialogService Dialog;
            internal readonly QuestSaveParticipant Save;
            internal int StoryIndex => Catalog.IndexOf(Story);
            internal Fixture()
            {
                Story = QuestServiceTests.Quest("quest_save", QuestServiceTests.Stage("first", "$story_count >= 1", "add_fact story_effects 1"));
                Contract = QuestServiceTests.Quest("contract_save", QuestServiceTests.Stage("contract", "$story_count >= 100"));
                QuestServiceTests.Field(Contract, "kind", QuestKind.Contract);
                QuestServiceTests.Field(Contract, "logEntries", new List<QuestLogEntry> { new() { id = "accepted", text = "Accepted" } });
                Beat = BeatServiceTests.Beat("beat_save", BeatTrigger.Manual, BeatRepeat.Always);
                Source(Beat.OnComplete, new[] { "add_fact story_effects 10" });
                Clue = TestContentFactory.Create<ClueDefinition>("clue_save");
                Rumor = TestContentFactory.Create<RumorDefinition>("rumor_save"); Source(Rumor.ResolvedWhen, "$story_count >= 1");
                var definitions = FactCatalogTests.BaseContent(); definitions.AddRange(new ContentDefinition[] { Story, Contract, Beat, Clue, Rumor,
                    FactCatalogTests.Facts("facts_save", new FactDeclaration { Name = "story_count", Kind = FactKind.Int }, new FactDeclaration { Name = "story_effects", Kind = FactKind.Int }) });
                Catalog = FactCatalogTests.Content(definitions); var refs = RefResolverRegistry.CreateDefault(Catalog); var report = new ValidationReport("QUST fixture");
                var factCatalog = FactCatalog.Build(Catalog, refs, report); Facts = new FactStore(factCatalog);
                var compiler = new LogicCompiler(factCatalog, refs, new LogicFunctionRegistry()); var binder = new LogicBinder(factCatalog, refs, report);
                foreach (var definition in definitions) if (definition is ILogicSourceOwner owner) { owner.CompileLogic(compiler, report.For(definition.IdString)); owner.BindLogic(binder); }
                Assert.IsFalse(report.HasErrors, report.ToText());
                var owners = new FakeLogicContext(factCatalog); var context = new LogicContext(Facts, owners, owners, owners, () => 7);
                Host = new NarrativeService(Facts, context); Quests = new QuestService(Catalog, Facts, context, Host); Clues = new ClueService(Catalog, Facts, context);
                Dialog = new DialogService(new FakeDialogRunner(FakeDialogRunner.Line("Page")), DialogServiceTests.Table(), context, new DialogMemory());
                Beats = new BeatService(Catalog, Facts, context, Dialog, _ => 4, _ => "test", 7);
                Host.Register(Quests); Host.Register(Clues); Host.Register(Beats); Host.BeginNewGame(); Host.SetWorldReady(true); Host.SetPlayerFree(false); Host.Tick();
                Save = new QuestSaveParticipant(Catalog, Facts, Quests, Clues, Beats);
            }
            internal byte[] Capture() { using var writer = new SaveWriter(); Save.Capture(writer); return writer.ToArray(); }
            internal void Populate()
            {
                int contract = Catalog.IndexOf(Contract);
                for (int i = 0; i < 58; i++) { Assert.IsTrue(Quests.Start(contract)); Quests.AddLog(contract, "accepted"); if (i < 57) Assert.IsTrue(Quests.Fail(contract)); }
                Quests.Start(StoryIndex); Quests.Track(StoryIndex);
                var records = new List<QuestService.SavedRecord>(); Quests.CaptureRecords(records);
                records[0].Value.AcceptBaselineNames = new[] { "removed_fact", "story_count" };
                records[0].Value.AcceptBaselineSlots = new[] { -1, Facts.Catalog.SlotOf("story_count") };
                records[0].Value.AcceptBaselineValues = new[] { 321, 0 };
                var raw = new QuestInstance { Instance = 0, Status = QuestStatus.Active, Stage = 0, DoneBits = 7, AcceptOrder = 60,
                    AcceptBaselineNames = new[] { "removed_fact" }, AcceptBaselineSlots = new[] { -1 }, AcceptBaselineValues = new[] { 123 },
                    StageBaselineNames = new[] { "removed_fact" }, StageBaselineSlots = new[] { -1 }, StageBaselineValues = new[] { 456 } };
                raw.Log.Add("unknown_log"); records.Add(new QuestService.SavedRecord(new ContentId("quest_unknown"), raw));
                records.Sort((a,b) => { int n = string.CompareOrdinal(a.Id.Value,b.Id.Value); return n != 0 ? n : a.Value.Instance.CompareTo(b.Value.Instance); });
                Quests.RestoreRecords(records, Story.Id);
                Beats.PerBody.Add((Beat.Id, 4, 19)); Beats.PerBody.Add((new ContentId("beat_unknown"), 9, 23));
                var scope = new LogicScope(LogicUsage.BeatEffect, 0, 0, Catalog.IndexOf(Beat), true, new LogicBodyHandle(4, 1, 2, 3, new ContentHash(91)));
                Beats.QueueState.Add(new BeatQueue.Entry(Catalog.IndexOf(Beat), scope, false));
                Beats.QueueState.Add(new BeatQueue.Entry(0, default, false, new ContentId("beat_unknown")));
                Clues.Reveal(Catalog.IndexOf(Clue)); Clues.Reveal(Catalog.IndexOf(Rumor));
                Clues.RestoreState(new[] { Clue.Id, new ContentId("clue_unknown") }, new[] { Rumor.Id, new ContentId("rumor_unknown") });
            }
            public void Dispose() { Dialog.Interrupt(); Beats.Dispose(); }
        }
        [Test] public void RoundTrip_ByteIdentical()
        {
            using var source = new Fixture(); source.Populate(); byte[] bytes = source.Capture();
            using var target = new Fixture(); target.Facts.Restore(source.Facts.Capture()); target.Save.Restore(new SaveReader(bytes), 1);
            var records = new List<QuestService.SavedRecord>(); target.Quests.CaptureRecords(records); Assert.AreEqual(60, records.Count);
            Assert.AreEqual(58, target.Quests.GetInstanceCount(target.Catalog.IndexOf(target.Contract)));
            Assert.AreEqual(target.StoryIndex, target.Quests.TrackedQuestIndex); Assert.AreEqual(2, target.Beats.QueuedCount);
            Assert.AreEqual(2, target.Beats.PerBody.Count); Assert.IsTrue(target.Clues.IsSeen(target.Catalog.IndexOf(target.Clue)));
            CollectionAssert.AreEqual(bytes, target.Capture()); target.Host.OnSessionRestored(); target.Host.Tick(); CollectionAssert.AreEqual(bytes, target.Capture());
        }
        [Test] public void Unknown_Preserved()
        {
            using var f = new Fixture(); f.Populate(); byte[] bytes = f.Capture(); f.Save.Restore(new SaveReader(bytes), 1);
            CollectionAssert.AreEqual(bytes, f.Capture());
            LogAssert.Expect(LogType.Error, new Regex("Unresolvable quest baseline fact"));
            Assert.AreEqual(0, f.Quests.GetBaseline(new LogicScope(LogicUsage.QuestObjective, f.Catalog.IndexOf(f.Contract), 0, 0, false, default), -1, BaselineKind.Accept));
            // Unknown queue entries stay owned even after all known entries have played.
            f.Beats.AfterEvaluation(true); Assert.AreEqual(1, f.Beats.QueuedCount); f.Beats.AfterEvaluation(true); Assert.AreEqual(1, f.Beats.QueuedCount);
            byte[] remaining = f.Capture(); f.Save.Restore(new SaveReader(remaining), 1); CollectionAssert.AreEqual(remaining, f.Capture());
            f.Save.Restore(SaveReader.Empty, 0); Assert.AreEqual(0, f.Beats.QueuedCount); Assert.AreEqual(0, f.Quests.GetInstanceCount(f.StoryIndex));
        }
        [Test] public void Restore_NoEffects()
        {
            using var f = new Fixture(); f.Populate(); byte[] bytes = f.Capture();
            int effect = f.Facts.Catalog.SlotOf("story_effects"); f.Facts.SetBySlot(f.Facts.Catalog.SlotOf("story_count"), 1, FactWriteSource.Debugger);
            int changes = 0; f.Quests.QuestChanged += _ => changes++; f.Clues.ClueChanged += _ => changes++;
            f.Save.Restore(new SaveReader(bytes), 1); Assert.AreEqual(0, changes); Assert.AreEqual(0, f.Facts.GetBySlot(effect));
            f.Host.OnSessionRestored(); f.Host.Tick(); Assert.AreEqual(0, f.Facts.GetBySlot(effect));
            Assert.AreEqual(QuestStatus.Active, f.Quests.GetStatus(f.StoryIndex)); Assert.IsFalse(f.Quests.IsObjectiveDone(f.StoryIndex, "first_objective"));
            f.Facts.SetBySlot(f.Facts.Catalog.SlotOf("story_count"), 2, FactWriteSource.Debugger); f.Host.Tick();
            Assert.AreEqual(QuestStatus.Completed, f.Quests.GetStatus(f.StoryIndex)); Assert.AreEqual(1, f.Facts.GetBySlot(effect));
        }
        [Test] public void Malformed_LeavesOwnersUnchanged()
        {
            using var f = new Fixture(); f.Populate(); byte[] bytes = f.Capture();
            for (int length = 0; length < bytes.Length; length += Math.Max(1, bytes.Length / 16))
            {
                Assert.Throws<SaveFormatException>(() => f.Save.Restore(new SaveReader(bytes, 0, length), 1));
                CollectionAssert.AreEqual(bytes, f.Capture(), "Truncation at byte " + length);
            }
            Assert.Throws<SaveFormatException>(() => f.Save.Restore(new SaveReader(bytes, 0, bytes.Length - 1), 1));
            CollectionAssert.AreEqual(bytes, f.Capture());
            var trailing = new byte[bytes.Length + 1]; Array.Copy(bytes, trailing, bytes.Length);
            Assert.Throws<SaveFormatException>(() => f.Save.Restore(new SaveReader(trailing), 1)); CollectionAssert.AreEqual(bytes, f.Capture());
            Assert.Throws<SaveFormatException>(() => f.Save.Restore(new SaveReader(bytes), 2)); CollectionAssert.AreEqual(bytes, f.Capture());
        }
    }
    public sealed class QuestSavePerformanceTests
    {
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        [Test, Category("Performance")] public void Capture_Time()
        {
            using var f = new QuestSaveTests.Fixture(); f.Populate(); using var writer = new SaveWriter();
            f.Save.Capture(writer); var samples = new double[10];
            for (int i = 0; i < samples.Length; i++) { writer.Reset(); long start = Stopwatch.GetTimestamp(); f.Save.Capture(writer); samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency; }
            PerfRun.Gate("QuestSave.Capture60", samples, .5);
        }
    }
}
