using System;
using System.Collections;
using System.IO;
using Game.Core.Save;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-009: the autosave state machine (SPEC-05 §5.9) driven with explicit frame times.</summary>
    public sealed class AutosaveSchedulerTests
    {
        static readonly SaveChunkRegistry TestRegistry = new SaveChunkRegistry(new[] { new ChunkRegistration("TSTA", 1000, "test", "a") });

        string root;
        SavePaths paths;
        SaveService service;
        FakeSaveContext context;
        FakePause pause;
        AutosaveScheduler auto;
        readonly ProfileId profile = new ProfileId("p001");

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "AutosaveSchedulerTests_" + Guid.NewGuid().ToString("N"));
            paths = new SavePaths(root);
            pause = new FakePause();
            service = new SaveService(paths, null, pause, new SaveServiceConfig(), TestRegistry);
            context = new FakeSaveContext();
            service.AttachContext(context);
            service.Register(new FakeParticipant("TSTA"));
            service.SetActiveProfile(profile);
            auto = service.Autosave;
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            Game.Core.Events.EventHub.Reset();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        IEnumerator WaitIdle()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (service.IsBusy && sw.Elapsed.TotalSeconds < 10) yield return null;
            Assert.IsFalse(service.IsBusy, "save finished");
        }

        SaveReason? AutoReason(int index)
        {
            var file = paths.MetaFile(new SlotRef(profile, new SlotId("auto_" + index)));
            if (!File.Exists(file)) return null;
            return new NewtonsoftSaveJsonCodec().Deserialize<SaveMeta>(File.ReadAllBytes(file)).Reason;
        }

        [UnityTest]
        public IEnumerator Timer_CountsUnpausedTimeOnly()
        {
            auto.Update(299f);
            Assert.IsFalse(service.IsBusy, "no save before 300 s");
            pause.Paused = true;
            auto.Update(100f);
            Assert.IsFalse(service.IsBusy, "paused time does not count");
            Assert.AreEqual(1f, auto.SecondsUntilTimerSave, 1e-3);
            pause.Paused = false;
            auto.Update(1f);
            Assert.IsTrue(service.IsBusy, "the timer save starts at 300 s");
            yield return WaitIdle();
            Assert.AreEqual(SaveReason.AutoTimer, AutoReason(0), "to the next auto slot");
            Assert.AreEqual(300f, auto.SecondsUntilTimerSave, 1e-3, "NotifySaved restarted the timer");
        }

        [UnityTest]
        public IEnumerator MinSpacing_DropsTimerAndStarSystem()
        {
            auto.Request(AutosaveTrigger.StarSystemEntered);
            auto.Update(0.1f);
            yield return WaitIdle();
            Assert.AreEqual(SaveReason.AutoStarSystemEntered, AutoReason(0));

            auto.Update(30f);
            auto.Request(AutosaveTrigger.Timer);
            auto.Request(AutosaveTrigger.StarSystemEntered);
            Assert.IsFalse(auto.HasPendingRequest, "within 60 s of the last save: ignored");
            auto.Update(31f);
            auto.Request(AutosaveTrigger.StarSystemEntered);
            Assert.IsTrue(auto.HasPendingRequest, "after 60 s: accepted");
        }

        [UnityTest]
        public IEnumerator SceneTransition_AlwaysSaves()
        {
            auto.Request(AutosaveTrigger.Timer);
            auto.Update(0.1f);
            yield return WaitIdle();
            auto.Request(AutosaveTrigger.SceneTransition);                 // right after a save
            Assert.IsTrue(auto.HasPendingRequest, "not subject to the spacing rule");
            auto.Update(0.1f);
            yield return WaitIdle();
            Assert.AreEqual(SaveReason.AutoSceneTransition, AutoReason(1), "the next auto slot");
        }

        [UnityTest]
        public IEnumerator Blocked_RetriesEveryTenSeconds()
        {
            context.Block = SaveBlockReason.InCombat;
            auto.Request(AutosaveTrigger.SceneTransition);
            int checks = context.BlockChecks;
            auto.Update(0.1f);
            Assert.AreEqual(checks + 1, context.BlockChecks, "checked once");
            for (int i = 0; i < 99; i++) auto.Update(0.1f);                // 9.9 s
            Assert.AreEqual(checks + 1, context.BlockChecks, "no re-check within 10 s");
            context.Block = SaveBlockReason.None;
            auto.Update(0.15f);
            auto.Update(0.01f);
            Assert.GreaterOrEqual(context.BlockChecks, checks + 2, "re-checked after 10 s (the save's own capture checks once more)");
            Assert.IsTrue(service.IsBusy, "and saves once allowed");
            yield return WaitIdle();
            Assert.AreEqual(SaveReason.AutoSceneTransition, AutoReason(0));
            Assert.IsFalse(auto.HasPendingRequest);
        }

        [Test]
        public void NoSession_ClearsPending()
        {
            auto.Request(AutosaveTrigger.SceneTransition);
            context.HasActiveSession = false;
            auto.Update(400f);
            Assert.IsFalse(auto.HasPendingRequest);
            Assert.IsFalse(service.IsBusy);
            Assert.AreEqual(300f, auto.SecondsUntilTimerSave, 1e-3, "elapsed reset");
        }

        [Test]
        public void Update_DoesNotAllocate()
        {
            auto.Update(1f);
            Assert.That(() => { auto.Update(0.016f); }, Is.Not.AllocatingGCMemory(), "idle");
            context.Block = SaveBlockReason.InCombat;
            auto.Request(AutosaveTrigger.SceneTransition);
            auto.Update(0.016f);
            Assert.That(() => { auto.Update(0.016f); }, Is.Not.AllocatingGCMemory(), "pending and blocked");
        }
    }
}
