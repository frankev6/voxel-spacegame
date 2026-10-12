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

        [UnityTest]
        public IEnumerator DisposeDuringThumbnailWait_ReleasesImmediately()
        {
            participant.WithDeferred = true;
            context.SlotThumbnail = new ThumbnailRequest { Key = "slot", Pending = true };
            Assert.IsFalse(service.HandleWantsToQuit());
            Assert.AreEqual(1, participant.Blocks.Count);
            var block = participant.Blocks[0];
            Assert.AreEqual(0, block.Disposes);
            service.Dispose();
            Assert.AreEqual(1, block.Disposes, "release cannot depend on another frame");
            Assert.AreEqual(0, block.Encodes);
            double deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 5;
            while (service.IsBusy && UnityEngine.Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsFalse(service.IsBusy);
            Assert.AreEqual(1, block.Disposes);
            Assert.AreEqual(0, block.Encodes);
            Assert.AreEqual(0, quits, "disposed quit routine cannot request application exit later");
            Assert.IsFalse(File.Exists(AutoMeta));
            Assert.IsTrue(service.HandleWantsToQuit());
        }

        sealed class GatedBlock : ISaveParticipant, ISaveDeferredBlock
        {
            public string Key => "TSTA";
            public int Version => 1;
            internal readonly System.Threading.ManualResetEventSlim Entered = new System.Threading.ManualResetEventSlim();
            internal readonly System.Threading.ManualResetEventSlim Resume = new System.Threading.ManualResetEventSlim();
            internal int Disposes;
            public void Capture(SaveWriter writer) => writer.WriteDeferred(this);
            public void Restore(SaveReader reader, int version) => throw new NotSupportedException();
            public byte[] Encode()
            {
                Entered.Set();
                if (!Resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("test encoder was not released");
                return new byte[] { 1, 2, 3 };
            }
            public void Dispose() => System.Threading.Interlocked.Increment(ref Disposes);
        }

        [UnityTest]
        public IEnumerator DisposeDuringEncoding_WorkerOwnsSnapshot()
        {
            service.Dispose();
            service = new SaveService(paths, null, null, new SaveServiceConfig(), TestRegistry);
            service.AttachContext(context);
            var block = new GatedBlock();
            service.Register(block);
            int completed = 0;
            service.OperationCompleted += _ => completed++;
            var pending = service.SaveAsync(new SlotRef(profile, new SlotId("manual_001")), SaveReason.Manual).GetAwaiter();
            double deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 5;
            try
            {
                while (!block.Entered.IsSet && UnityEngine.Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsTrue(block.Entered.IsSet, "worker reached encoding");
                service.Dispose();
                Assert.AreEqual(0, block.Disposes, "worker still owns its live encoding input");
            }
            finally { block.Resume.Set(); }
            deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 5;
            while (!pending.IsCompleted && UnityEngine.Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(pending.IsCompleted);
            Assert.IsTrue(pending.GetResult().Success, "already owned write finishes atomically");
            Assert.AreEqual(1, block.Disposes);
            Assert.AreEqual(0, completed, "disposed service does not publish completion");
            block.Entered.Dispose(); block.Resume.Dispose();
        }
    }
}
