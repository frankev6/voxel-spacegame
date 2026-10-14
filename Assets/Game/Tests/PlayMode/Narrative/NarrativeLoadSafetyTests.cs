using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.App.Boot;
using Game.App.DevRuntime;
using Game.App.Flow;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Economy;
using Game.Galaxy.Services;
using Game.Narrative;
using Game.Narrative.Beats;
using Game.Narrative.Clues;
using Game.Narrative.Dialog;
using Game.Narrative.Quests;
using Game.Persistence.Data;
using Game.Presentation.Bridges;
using Game.Tests.Common;
using Game.UI.Services;
using Game.UI.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Narrative
{
    /// <summary>NAR-019: actual FACT/QUST/DLGS owners, physical interactions and restore boundaries.</summary>
    public sealed class NarrativeLoadSafetyTests : GameFlowFixture
    {
        const string SavedFact = "intro_awake", UnknownFact = "unknown_nar019_saved_fact";
        GameComposition Game => GameRoot.Instance.Composition;
        NarrativeService Narrative => Service<NarrativeService>();
        FactStore Facts => Narrative.Facts;
        ContentCatalog Catalog => Service<ContentService>().Catalog;
        DialogService Dialog => Service<DialogService>();
        UIRoot UI => Service<UIRoot>();

        // Suspend only initial automatic opening evaluation. The scenarios re-enable the
        // actual systems after initialization, before scripting, and never reset on load.
        // This avoids re-running the full combat chapter in each save-safety fixture.
        sealed class InitialSceneFixture : ISceneReady
        {
            public int SceneReadyOrder => SceneHookOrder.Session + 1;
            public void OnSceneReady(in SceneReadyContext context)
            {
                if (context.Scene != GameScene.World || context.Reason != SceneEntryReason.NewGame) return;
                Services.Get<NarrativeService>().BeginNewGame(false);
                Services.Get<EncounterDirectorService>().Enabled = false;
            }
            public bool IsSceneReadyComplete(in SceneReadyContext context) => true;
        }
        IEnumerator StartWorld()
        {
            Assert.IsTrue(Services.TryGet<QuestService>(out _), "QST-008 must register the real quest owner.");
            Assert.IsTrue(Services.TryGet<ClueService>(out _), "QST-008 must register the real clue owner.");
            Assert.IsTrue(Services.TryGet<BeatService>(out _), "QST-008 must register the real beat owner.");
            Assert.IsNotNull(Dialog, "DLG-007 must register the actual compiled dialog owner.");
            var flow = Service<SceneFlow>(); var initial = new InitialSceneFixture(); flow.RegisterReady(initial);
            try { yield return NewGame(19); }
            finally { flow.UnregisterReady(initial); }
            Dialog.Interrupt();
            // Establish a post-opening baseline via canonical lifecycle callbacks. Clear
            // incidental initial arrival requests, then enable real quest/beat evaluation.
            Narrative.BeginNewGame(false); Narrative.OnSessionRestored();
            yield return null;
            Assert.IsTrue(PlayerReady); Assert.IsFalse(Dialog.IsActive);
        }

        // Retained from the previous delivery: observation only, before production
        // systems on the first restored evaluation. It creates no effects or callbacks.
        sealed class FirstReadyObserver : INarrativeSystem
        {
            readonly FactStore store;
            readonly Func<int> delivered;
            readonly LogicTraceEntry[] trace = new LogicTraceEntry[LogicTraceLog.Capacity];
            int startVersion, slot, deferredSlot;
            bool armed, restored;
            internal bool Observed, FullReevaluate;
            internal int Delivered, AppliedEffects, EffectFactWrites, RestoredValue, DeferredValue, TraceCount;
            internal FactSnapshot Snapshot;
            internal FirstReadyObserver(FactStore store, Func<int> delivered) { this.store = store; this.delivered = delivered; }
            public int Order => int.MinValue;
            internal void Arm(int factSlot, int deferredFactSlot)
            { slot = factSlot; deferredSlot = deferredFactSlot; startVersion = store.Trace.Version; armed = true; restored = Observed = false; }
            public void OnNewGame() { armed = restored = false; }
            public void OnRestored() => restored = true;
            public void Evaluate(IReadOnlyList<LogicDependency> changed, bool fullReevaluate)
            {
                if (!armed || !restored || Observed) return;
                Delivered = delivered(); RestoredValue = store.GetBySlot(slot); DeferredValue = store.GetBySlot(deferredSlot);
                FullReevaluate = fullReevaluate; Snapshot = store.Capture();
                TraceCount = unchecked(store.Trace.Version - startVersion);
                int count = store.Trace.CopyTo(trace), start = Math.Max(0, count - TraceCount);
                for (int i = start; i < count; i++)
                {
                    if (trace[i].Kind == LogicTraceKind.EffectApplied) AppliedEffects++;
                    if (trace[i].Kind == LogicTraceKind.FactWrite && trace[i].Source == FactWriteSource.Effect) EffectFactWrites++;
                }
                Observed = true;
            }
            public void AfterEvaluation(bool playerFree) { }
        }

        [UnityTest] public IEnumerator Load_ExecutesNoEffects()
        {
            int delivered = 0;
            var observer = new FirstReadyObserver(Facts, () => delivered); Narrative.Register(observer);
            yield return StartWorld();
            int slot = Slot(SavedFact), deferred = Slot("opening_chapter_done");
            Assert.AreEqual(FactScope.Save, Facts.Catalog.ScopeAt(slot));
            var survey = Catalog.Get<QuestDefinition>(new ContentId("quest_survey_local_materials"));
            int traceStart = Facts.Trace.Version;
            Assert.IsTrue(Service<QuestService>().Start(Catalog.IndexOf(survey)));
            Assert.Greater(TraceSince(traceStart, LogicTraceKind.EffectApplied), 0,
                "Positive control: actual non-fact quest acceptance effects produce EffectApplied.");

            var pauseView = UI.Navigator.Get<PauseView>(); UI.Navigator.Push(pauseView);
            yield return WaitUntil(() => pauseView.IsOpen && Game.Pause.IsPaused, 5, "Actual pause state opens");
            yield return null; // real bridge observes the pause layer before the condition is made true
            Facts.SetBySlot(slot, 1, FactWriteSource.Debugger);
            Facts.SetBySlot(Slot("gate_ark_start_active"), 1, FactWriteSource.Debugger);
            EventHub.DrainAll(); Narrative.Tick(); EventHub.DrainAll();
            Assert.AreEqual(0, Facts.GetBySlot(deferred), "The modal pause keeps the real gate effect queued.");
            Assert.Greater(Service<BeatService>().QueuedCount, 0, "The saved real beat queue is nonempty.");
            var saved = Facts.Capture();
            yield return Complete(Game.Session.SaveGameAsync("manual_019", SaveReason.Manual), "Save facts and actual quest/beat state");
            Facts.SetBySlot(slot, 0, FactWriteSource.Debugger); Facts.SetBySlot(deferred, 1, FactWriteSource.Debugger);
            EventHub.DrainAll(); Narrative.Tick(); EventHub.DrainAll();
            Action<FactChanged> onFact = _ => { if (!observer.Observed) delivered++; };
            EventBus<FactChanged>.Subscribe(onFact);
            try
            {
                observer.Arm(slot, deferred);
                // Keep simulation paused even when World readiness closes the old pause view.
                using var pause = Game.Pause.Acquire("NAR-019 load boundary");
                Assert.IsTrue(pauseView.IsOpen); Assert.IsTrue(Game.Pause.IsPaused);
                yield return Complete(Game.Session.LoadGameAsync("manual_019"), "Actual load from pause");
                yield return WaitUntil(() => observer.Observed, 5, "First restored narrative evaluation is observed");
                Assert.IsTrue(observer.FullReevaluate);
                Assert.AreEqual(1, observer.RestoredValue); Assert.AreEqual(0, observer.DeferredValue);
                CollectionAssert.AreEqual(saved.Entries, observer.Snapshot.Entries, "Observe the exact restored FACT before ready-tick effects.");
                Assert.AreEqual(0, observer.Delivered, "No restore/entry FactChanged is delivered before the first ready tick.");
                Assert.LessOrEqual(observer.TraceCount, LogicTraceLog.Capacity, "The observation interval must not overflow the trace ring.");
                Assert.AreEqual(0, observer.AppliedEffects); Assert.AreEqual(0, observer.EffectFactWrites);
                yield return WaitUntil(() => Facts.GetBySlot(deferred) == 1, 5, "The genuine queued gate effect executes after the boundary");
                Assert.Greater(TraceSince(traceStart, LogicTraceKind.EffectApplied), 1,
                    "The live beat effects run after restoration; absence before the boundary was not a disconnected owner.");
                Assert.AreEqual(QuestStatus.Active, Service<QuestService>().GetStatus(Catalog.IndexOf(survey)));
            }
            finally { EventBus<FactChanged>.Unsubscribe(onFact); }
        }

        [UnityTest] public IEnumerator SameSeedSameInputs_SameFacts()
        {
            var snapshots = new FactSnapshot[2];
            for (int run = 0; run < snapshots.Length; run++)
            {
                yield return StartWorld();
                int acquired = Facts.Catalog.GeneratedSlot(FactFamily.Acquired, new ContentId("resource_fuel").Hash.Value);
                Assert.GreaterOrEqual(acquired, 0); int before = Facts.GetBySlot(acquired);
                var economy = Service<EconomyService>();
                var fuel = Catalog.Get<ItemDefinition>(new ContentId("resource_fuel"));
                economy.GiveItems(Catalog.IndexOf(fuel), 2);
                economy.GiveLoot(Catalog.IndexOf(Catalog.Get<LootTableDefinition>(new ContentId("loot_salvage_module"))), 2, 19);
                yield return null;

                // Keep the actual gameplay pipeline running: the interaction command
                // moves the player and physics/spatial data must refresh before F.
                // These scripted rewards/counters depend on seed and inputs, not time.
                yield return InteractStation();
                yield return Choice("Mission Control", 4); yield return Choice("Accept Material Turn-In Test", 1);
                yield return Choice("Undock", 7); yield return Undock();
                yield return InteractStation();
                yield return Choice("Station Bar", 2); yield return Choice("Talk to Shady Figure", 1);
                yield return Choice("Pay for Info (100 Scrap)", 1); yield return Choice("Thanks", 1);
                yield return Choice("Back", 2); yield return Choice("Undock", 7); yield return Undock();
                EventHub.DrainAll(); Narrative.Tick(); EventHub.DrainAll();
                Assert.AreEqual(2, Facts.GetBySlot(acquired) - before, "Actual acquired-item events reach the bridge.");
                int station = Facts.Catalog.GeneratedSlot(FactFamily.Interacted, new ContentId("body_station_ark").Hash.Value);
                Assert.GreaterOrEqual(station, 0); Assert.AreEqual(2, Facts.GetBySlot(station));
                var survey = Catalog.Get<QuestDefinition>(new ContentId("quest_survey_local_materials"));
                Assert.AreEqual(QuestStatus.Active, Service<QuestService>().GetStatus(Catalog.IndexOf(survey)));
                var rumor = Catalog.Get<ClueDefinition>(new ContentId("rumor_hidden_cache"));
                Assert.AreEqual(1, Service<ClueService>().OrdinalOf(Catalog.IndexOf<ClueDefinition>(rumor)));
                Assert.AreEqual(2, Facts.GetBySlot(Slot("beat_station_hub")));
                snapshots[run] = Facts.Capture();
                Assert.Greater(snapshots[run].Entries.Count, 5, "The real script produces nontrivial saved state.");
            }
            CollectionAssert.AreEqual(snapshots[0].Entries, snapshots[1].Entries,
                "Same seed and physical/scripted inputs produce identical ordered names and values.");
        }

        [UnityTest] public IEnumerator UnknownFact_SurvivesResave()
        {
            yield return StartWorld();
            Assert.Less(Facts.Catalog.SlotOf(UnknownFact), 0, "The fixture represents removed content.");
            Facts.SetBySlot(Slot(SavedFact), 1, FactWriteSource.Debugger);
            var previousContent = Facts.Capture(); previousContent.Entries.Add(new FactEntry(UnknownFact, 731));
            Facts.Restore(previousContent); // canonical arrangement of prior-version content; no fake save owner
            var expected = Facts.Capture();
            yield return Complete(Game.Session.SaveGameAsync("manual_020", SaveReason.Manual), "Save real FACT with removed content");
            byte[] original = null; yield return ReadFactChunk("manual_020", payload => original = payload);
            AssertUnknown(original);
            Facts.ResetToDefaults(); Assert.IsFalse(ContainsUnknown(Facts.Capture()));
            yield return Complete(Game.Session.LoadGameAsync("manual_020"), "Load actual unknown FACT data");
            CollectionAssert.AreEqual(expected.Entries, Facts.Capture().Entries);
            Assert.IsTrue(ContainsUnknown(Facts.Capture()));
            yield return Complete(Game.Session.SaveGameAsync("manual_021", SaveReason.Manual), "Re-save restored unknown fact");
            byte[] savedAgain = null; yield return ReadFactChunk("manual_021", payload => savedAgain = payload);
            AssertUnknown(savedAgain);
            CollectionAssert.AreEqual(original, savedAgain, "The actual FACT chunk, including the orphan, re-saves byte-identically.");
        }

        IEnumerator InteractStation()
        {
            var host = Service<DevRuntimeHost>(); var result = host.Commands.Execute("interact body=ark_station", host.Context);
            Assert.AreEqual(DevResultKind.Pending, result.Kind, result.Text);
            Assert.IsTrue(host.TryGetOperation(result.OperationId, out var operation));
            yield return WaitUntil(() => operation.Status != DevOperationStatus.Running, 10, "Actual station F input completes");
            Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status, operation.Message);
            yield return WaitUntil(() => Dialog.IsActive, 5, "The compiled station beat starts");
            Assert.AreEqual("station_hub_hub", Dialog.CurrentNode);
            Assert.IsTrue(Service<DockingService>().IsDocked);
        }
        IEnumerator Choice(string text, int number)
        {
            var model = Service<DialogPresenter>().Model;
            yield return WaitUntil(() => Dialog.IsActive && model.IsWaitingForChoice &&
                model.Options.Any(option => option.Text == text && option.Enabled && option.Number == number), 5, "Actual option " + text);
            var host = Service<DevRuntimeHost>(); host.Input.SetKeys("Digit" + number, .01, false);
            yield return null;
            host.Input.SetKeys("Digit" + number, 0, true);
            yield return null; // explicit release permits adjacent identical number choices at fast frame rates
        }
        IEnumerator Undock()
        {
            yield return WaitUntil(() => !Dialog.IsActive, 5, "Compiled hub ends before the next input");
            Service<DockingService>().Undock();
            yield return WaitUntil(() => PlayerReady, 5, "Actual dock owner restores Flight input");
        }
        int Slot(string name)
        { int slot = Facts.Catalog.SlotOf(name); Assert.GreaterOrEqual(slot, 0, name); return slot; }
        int TraceSince(int version, LogicTraceKind kind)
        {
            var trace = new LogicTraceEntry[LogicTraceLog.Capacity]; int copied = Facts.Trace.CopyTo(trace);
            int delta = unchecked(Facts.Trace.Version - version);
            Assert.LessOrEqual(delta, LogicTraceLog.Capacity, "Trace interval must fit the bounded log.");
            int result = 0;
            for (int i = Math.Max(0, copied - delta); i < copied; i++) if (trace[i].Kind == kind) result++;
            return result;
        }
        IEnumerator ReadFactChunk(string slot, Action<byte[]> receive)
        {
            var awaiter = Game.Save.ReadAsync(new SlotRef(Game.Save.ActiveProfile, new SlotId(slot))).GetAwaiter();
            yield return WaitUntil(() => awaiter.IsCompleted, 10, "Read actual FACT chunk " + slot);
            var loaded = awaiter.GetResult(); Assert.IsTrue(loaded.Success, loaded.Message);
            Assert.IsFalse(loaded.Save.Report.HasErrors, loaded.Save.Report.ToText());
            var chunk = loaded.Save.Chunks.Single(value => value.Key == "FACT");
            Assert.AreEqual(1, chunk.Version); Assert.IsFalse(chunk.Corrupt); receive(chunk.Payload);
        }
        static void AssertUnknown(byte[] payload)
        {
            var reader = new SaveReader(payload); int count = reader.ReadInt32(); bool found = false; string previous = null;
            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString(); int value = reader.ReadInt32();
                Assert.IsTrue(previous == null || StringComparer.Ordinal.Compare(previous, name) < 0, "Stored FACT remains ordinal and unique.");
                if (name == UnknownFact) { Assert.AreEqual(731, value); found = true; }
                previous = name;
            }
            Assert.IsTrue(reader.IsAtEnd); Assert.IsTrue(found, "The actual disk payload retains the unknown name/value.");
        }
        static bool ContainsUnknown(FactSnapshot snapshot)
            => snapshot.Entries.Any(entry => entry.Name == UnknownFact && entry.Value == 731);
    }
}
