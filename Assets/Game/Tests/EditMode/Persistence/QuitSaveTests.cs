using System;
using System.Collections;
using System.IO;
using Game.Core.Save;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-009: the quit hook (SPEC-05 §5.9). Application.wantsToQuit is not raised in edit mode; the tests call the handler.</summary>
    public sealed class QuitSaveTests
    {
        static readonly SaveChunkRegistry TestRegistry = new SaveChunkRegistry(new[] { new ChunkRegistration("TSTA", 1000, "test", "a") });

        string root;
        SavePaths paths;
        SaveService service;
        FakeSaveContext context;
        FakeParticipant participant;
        int quits;
        readonly ProfileId profile = new ProfileId("p001");

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "QuitSaveTests_" + Guid.NewGuid().ToString("N"));
            paths = new SavePaths(root);
            service = new SaveService(paths, null, null, new SaveServiceConfig { QuitSaveTimeoutSeconds = 0.5f }, TestRegistry);
            context = new FakeSaveContext();
            service.AttachContext(context);
            participant = new FakeParticipant("TSTA");
            service.Register(participant);
            service.SetActiveProfile(profile);
            quits = 0;
            service.QuitAction = () => quits++;
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            Game.Core.Events.EventHub.Reset();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        string AutoMeta => paths.MetaFile(new SlotRef(profile, new SlotId("auto_0")));

        [Test]
        public void NoSession_QuitsImmediately()
        {
            context.HasActiveSession = false;
            Assert.IsTrue(service.HandleWantsToQuit());
            Assert.AreEqual(0, quits, "Unity quits by itself when the handler approves");
        }

        [Test]
        public void Blocked_QuitsWithoutSave()
        {
            context.Block = SaveBlockReason.InCombat;
            Assert.IsTrue(service.HandleWantsToQuit());
            Assert.IsFalse(File.Exists(AutoMeta));
        }

        [UnityTest]
        public IEnumerator Allowed_SavesThenQuits()
        {
            Assert.IsFalse(service.HandleWantsToQuit(), "quit postponed for the save");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (quits == 0 && sw.Elapsed.TotalSeconds < 5) yield return null;
            Assert.AreEqual(1, quits, "quits after the save");
            Assert.AreEqual(SaveReason.Quit, new NewtonsoftSaveJsonCodec().Deserialize<SaveMeta>(File.ReadAllBytes(AutoMeta)).Reason);
            Assert.IsTrue(service.HandleWantsToQuit(), "the second request is approved");
        }

        [UnityTest]
        public IEnumerator Timeout_QuitsAnyway()
        {
            participant.WithDeferred = true; participant.DeferredSleepMs = 2000;   // longer than the 0.5 s timeout
            var sw = System.Diagnostics.Stopwatch.StartNew();
            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("Quit save did not finish"));
            Assert.IsFalse(service.HandleWantsToQuit());
            while (quits == 0 && sw.Elapsed.TotalSeconds < 5) yield return null;
            Assert.AreEqual(1, quits);
            Assert.Less(sw.Elapsed.TotalSeconds, 1.5, "abandoned at the timeout, not after the save");
            Assert.IsTrue(service.IsBusy, "the hanging save is still running");
            while (service.IsBusy && sw.Elapsed.TotalSeconds < 10) yield return null;   // let it finish before cleanup
        }
    }
}
