using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core.Events;
using Game.Core.Save;
using Game.Data.Validation;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-005: the save pipeline (SPEC-05 §5.3, §5.4, §5.8) on a temporary save root with test chunks TSTA/TSTB/TSTC.</summary>
    public sealed class SavePipelineTests
    {
        static readonly SaveChunkRegistry TestRegistry = new SaveChunkRegistry(new[]
        {
            new ChunkRegistration("TSTA", 1000, "test", "a"), new ChunkRegistration("TSTB", 1010, "test", "b"), new ChunkRegistration("TSTC", 1020, "test", "c"),
        });

        string root;
        SavePaths paths;
        SaveService service;
        FakeSaveContext context;
        FakeParticipant a, b, c;
        readonly List<SaveOperationResult> completed = new List<SaveOperationResult>();
        readonly SlotRef slot = new SlotRef(new ProfileId("p001"), new SlotId("manual_001"));

        [SetUp]
        public void SetUp()
        {
            EventHub.Reset();
            FakeDeferredBlock.ResetLive();
            root = Path.Combine(Path.GetTempPath(), "SavePipelineTests_" + Guid.NewGuid().ToString("N"));
            paths = new SavePaths(root);
            service = new SaveService(paths, null, null, new SaveServiceConfig(), TestRegistry);
            context = new FakeSaveContext();
            service.AttachContext(context);
            c = new FakeParticipant("TSTC"); a = new FakeParticipant("TSTA", 3); b = new FakeParticipant("TSTB", 2);
            service.Register(c); service.Register(a); service.Register(b);   // registration order does not matter
            completed.Clear();
            service.OperationCompleted += r => completed.Add(r);
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            EventHub.Reset();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        SaveResult SaveNow(SaveReason reason = SaveReason.Manual)
        {
            var captured = service.CaptureNow(slot, reason, null);
            return service.WriteCaptured(captured);
        }

        static List<RawChunk> ReadChunks(string file, out ContainerHeader header)
        {
            var chunks = new List<RawChunk>();
            Assert.AreEqual(ContainerReadStatus.Ok, SaveContainerReader.Read(File.ReadAllBytes(file), out header, chunks, new ValidationReport("t"), file));
            return chunks;
        }

        Dictionary<string, byte[]> Snapshot()
        {
            var d = new Dictionary<string, byte[]>();
            foreach (var f in Directory.GetFiles(paths.SlotDir(slot), "*", SearchOption.AllDirectories)) d[f] = File.ReadAllBytes(f);
            return d;
        }

        static void AssertSameFiles(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
        {
            CollectionAssert.AreEquivalent(before.Keys, after.Keys, "no file added or removed (no .tmp left)");
            foreach (var kv in before) CollectionAssert.AreEqual(kv.Value, after[kv.Key], Path.GetFileName(kv.Key) + " unchanged");
        }

        [Test]
        public void Save_WritesChunksInRegistryOrder()
        {
            var r = SaveNow();
            Assert.IsTrue(r.Success, r.Message);
            Assert.AreEqual("Game saved.", r.Message);
            var chunks = ReadChunks(paths.WorldFile(slot), out var header);
            CollectionAssert.AreEqual(new[] { "META", "IDPL", "TSTA", "TSTB", "TSTC" }, chunks.Select(x => x.Key));
            CollectionAssert.AreEqual(new ushort[] { 1, 1, 3, 2, 1 }, chunks.Select(x => x.Version));
            Assert.AreEqual(r.Bytes, new FileInfo(paths.WorldFile(slot)).Length);

            var palette = SaveIdPalette.Parse(chunks[1].Payload);
            CollectionAssert.AreEqual(new[] { "block_tsta", "block_tstb", "block_tstc" }, Enumerable.Range(0, palette.Count).Select(i => palette.Get((ushort)i).Value), "palette in capture order");

            var json = new NewtonsoftSaveJsonCodec();
            var fromChunk = json.Deserialize<SaveMeta>(chunks[0].Payload);
            var fromFile = json.Deserialize<SaveMeta>(File.ReadAllBytes(paths.MetaFile(slot)));
            Assert.AreEqual(0, fromChunk.WorldBytes);
            Assert.AreEqual(r.Bytes, fromFile.WorldBytes);
            fromFile.WorldBytes = 0;
            CollectionAssert.AreEqual(json.Serialize(fromChunk), json.Serialize(fromFile), "meta.json equals META except WorldBytes");
            Assert.AreEqual("Save 1", fromFile.DisplayName);
            Assert.AreEqual("Tester", fromFile.CommanderName);
            Assert.AreEqual(42.5, header.PlaytimeSeconds);
            Assert.AreEqual(fromFile.SavedUtc.Ticks, header.SavedUtcTicks);
        }

        [UnityTest]
        public IEnumerator SaveAsync_RaisesCompletedAndGameSaved()
        {
            var saved = new List<GameSaved>();
            Action<GameSaved> onSaved = e => saved.Add(e);
            EventBus<GameSaved>.Subscribe(onSaved);
            try
            {
                var op = service.SaveAsync(slot, SaveReason.Quick, "Named").GetAwaiter();
                while (!op.IsCompleted) yield return null;
                var r = op.GetResult();
                Assert.IsTrue(r.Success, r.Message);
                EventHub.DrainAll();
                Assert.AreEqual(1, saved.Count); Assert.AreEqual("manual_001", saved[0].SlotId); Assert.AreEqual(SaveReason.Quick, saved[0].Reason);
                Assert.AreEqual(1, completed.Count); Assert.IsTrue(completed[0].Success); Assert.AreEqual("Game saved.", completed[0].Message);
                Assert.IsFalse(service.IsBusy);
                Assert.IsTrue(service.HasCurrentSlot); Assert.AreEqual(slot, service.CurrentSlot, "step 17");
                Assert.AreEqual("Named", new NewtonsoftSaveJsonCodec().Deserialize<SaveMeta>(File.ReadAllBytes(paths.MetaFile(slot))).DisplayName);
            }
            finally { EventBus<GameSaved>.Unsubscribe(onSaved); }
        }

        [Test]
        public void SecondSave_CreatesBackup()
        {
            Assert.IsTrue(SaveNow().Success);
            var first = File.ReadAllBytes(paths.WorldFile(slot));
            a.Value = 99;
            Assert.IsTrue(SaveNow().Success);
            CollectionAssert.AreEqual(first, File.ReadAllBytes(paths.BackupFile(slot)), "the first world.sav moved to world.bak byte-for-byte");
            CollectionAssert.AreNotEqual(first, File.ReadAllBytes(paths.WorldFile(slot)));
            Assert.IsFalse(File.Exists(paths.TempFile(slot)));
        }

        [Test]
        public void CaptureFailure_LeavesFilesUntouched()
        {
            Assert.IsTrue(SaveNow().Success);
            a.Value = 1; Assert.IsTrue(SaveNow().Success);            // world.sav, world.bak and meta.json all exist
            var before = Snapshot();
            a.WithDeferred = true; b.WithDeferred = true; b.ThrowOnCapture = true;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("capture of chunk TSTB"));
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("capture failure for the test"));
            var r = SaveNow();
            Assert.IsFalse(r.Success);
            Assert.AreEqual(SaveErrorKind.ParticipantFailed, r.Error);
            Assert.AreEqual("The game could not be saved because of an internal error (TSTB). Your previous save is unchanged.", r.Message);
            AssertSameFiles(before, Snapshot());
            Assert.AreEqual(0, FakeDeferredBlock.Live, "deferred blocks captured before the failure are disposed");
            Assert.AreEqual(0, a.Blocks[0].Encodes, "never encoded");
        }

        [TestCase("session preparation")]
        [TestCase("session metadata")]
        [TestCase("slot thumbnail")]
        [TestCase("session thumbnails")]
        public void ContextCaptureFailure_LeavesFilesUntouched(string stage)
        {
            Assert.IsTrue(SaveNow().Success);
            a.Value = 1; Assert.IsTrue(SaveNow().Success);
            var before = Snapshot();
            a.WithDeferred = true; b.WithDeferred = true; c.WithDeferred = true;
            context.ThrowAt = stage;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("capture of " + stage));
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("context capture failure: " + stage));
            var result = SaveNow();
            Assert.AreEqual(SaveErrorKind.ParticipantFailed, result.Error);
            StringAssert.Contains(stage, result.Message);
            AssertSameFiles(before, Snapshot());
            Assert.AreEqual(0, FakeDeferredBlock.Live);
            foreach (var participant in new[] { a, b, c })
            {
                if (participant.Blocks.Count == 0) continue; // preparation failed before any snapshots existed
                Assert.AreEqual(0, participant.Blocks[0].Encodes);
                Assert.AreEqual(1, participant.Blocks[0].Disposes);
            }
        }

        [UnityTest]
        public IEnumerator ContextCaptureFailure_AsyncCompletesAndRecovers()
        {
            a.WithDeferred = true;
            foreach (string stage in new[] { "session preparation", "session metadata", "slot thumbnail", "session thumbnails" })
            {
                completed.Clear();
                context.ThrowAt = stage;
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("capture of " + stage));
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("context capture failure: " + stage));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("failed: ParticipantFailed"));
                var failure = service.SaveAsync(slot, SaveReason.Manual).GetAwaiter();
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
                while (!failure.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.IsTrue(failure.IsCompleted);
                Assert.AreEqual(SaveErrorKind.ParticipantFailed, failure.GetResult().Error);
                Assert.IsFalse(service.IsBusy);
                Assert.AreEqual(1, completed.Count); Assert.IsFalse(completed[0].Success);
                Assert.AreEqual(0, FakeDeferredBlock.Live);
                context.ThrowAt = null;
                var success = service.SaveAsync(slot, SaveReason.Manual).GetAwaiter();
                deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
                while (!success.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.IsTrue(success.IsCompleted); Assert.IsTrue(success.GetResult().Success);
                Assert.IsFalse(service.IsBusy);
                Assert.AreEqual(2, completed.Count); Assert.IsTrue(completed[1].Success);
                Assert.AreEqual(0, FakeDeferredBlock.Live);
            }
        }

        [Test]
        public void WriteFailure_LeavesPreviousSave()
        {
            Assert.IsTrue(SaveNow().Success);
            a.Value = 2; Assert.IsTrue(SaveNow().Success);
            var before = Snapshot();
            a.Value = 3; a.WithDeferred = true;
            service.TestHookBeforeReplace = () => throw new IOException("simulated disk failure");
            var r = SaveNow();
            service.TestHookBeforeReplace = null;
            Assert.IsFalse(r.Success);
            Assert.AreEqual(SaveErrorKind.IoError, r.Error);
            StringAssert.Contains("simulated disk failure", r.Message);
            AssertSameFiles(before, Snapshot());
            Assert.IsFalse(File.Exists(paths.TempFile(slot)), "no .tmp file");
            Assert.AreEqual(0, FakeDeferredBlock.Live);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MetadataWriteFailure_ReportsCommittedSave(bool overwrite)
        {
            byte[] previous = null;
            if (overwrite) { Assert.IsTrue(SaveNow().Success); previous = File.ReadAllBytes(paths.WorldFile(slot)); }
            Directory.CreateDirectory(paths.MetaFile(slot) + ".tmp"); // real I/O failure, after the container replacement
            a.Value = 99; a.WithDeferred = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Save committed, but metadata"));
            var captured = service.CaptureNow(slot, SaveReason.Manual, "Committed");
            var result = service.WriteCaptured(captured);
            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(0, FakeDeferredBlock.Live);
            var chunks = ReadChunks(paths.WorldFile(slot), out _);
            Assert.AreEqual(99, new SaveReader(chunks.Single(x => x.Key == "TSTA").Payload).ReadInt32());
            if (overwrite) CollectionAssert.AreEqual(previous, File.ReadAllBytes(paths.BackupFile(slot)));
            else Assert.IsFalse(File.Exists(paths.BackupFile(slot)));
            var listed = service.ListSlots(slot.Profile).Single();
            Assert.AreEqual(SlotStatus.MetaMissing, listed.Status);
            Assert.AreEqual("Committed", listed.Meta.DisplayName);
            Assert.AreEqual(result.Bytes, listed.Meta.WorldBytes);
            Assert.IsFalse(File.Exists(paths.TempFile(slot)));
        }

        [Test]
        public void StaleSidecar_UsesEmbeddedMeta()
        {
            Assert.IsTrue(SaveNow().Success);
            byte[] oldSidecar = File.ReadAllBytes(paths.MetaFile(slot));
            var oldMeta = new NewtonsoftSaveJsonCodec().Deserialize<SaveMeta>(oldSidecar);
            var captured = service.CaptureNow(slot, SaveReason.Manual, "Save 2"); // same string length
            captured.Meta.SavedUtc = oldMeta.SavedUtc;
            captured.Header.SavedUtcTicks = oldMeta.SavedUtc.Ticks;
            Assert.IsTrue(service.WriteCaptured(captured).Success);
            File.WriteAllBytes(paths.MetaFile(slot), oldSidecar);
            var listed = service.ListSlots(slot.Profile).Single();
            Assert.AreEqual(SlotStatus.MetaMissing, listed.Status);
            Assert.AreEqual("Save 2", listed.Meta.DisplayName);
            Assert.AreEqual(oldMeta.SavedUtc, listed.Meta.SavedUtc, "timestamp alone cannot detect stale metadata");
        }

        [UnityTest]
        public IEnumerator MetadataWriteFailure_AsyncReportsSuccess()
        {
            Assert.IsTrue(SaveNow().Success);
            Directory.CreateDirectory(paths.MetaFile(slot) + ".tmp");
            var saved = new List<GameSaved>();
            Action<GameSaved> handler = e => saved.Add(e);
            EventBus<GameSaved>.Subscribe(handler);
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Save committed, but metadata"));
                var operation = service.SaveAsync(slot, SaveReason.Manual, "Committed").GetAwaiter();
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
                while (!operation.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.IsTrue(operation.IsCompleted); Assert.IsTrue(operation.GetResult().Success);
                Assert.AreEqual(1, completed.Count); Assert.IsTrue(completed[0].Success);
                EventHub.DrainAll(); Assert.AreEqual(1, saved.Count);
                Assert.AreEqual(slot, service.CurrentSlot); Assert.IsFalse(service.IsBusy);
                Assert.AreEqual("Committed", service.ListSlots(slot.Profile).Single().Meta.DisplayName);
            }
            finally { EventBus<GameSaved>.Unsubscribe(handler); }
        }

        [Test]
        public void DeferredBlocks_AreDisposedOnAllPaths()
        {
            a.WithDeferred = true; c.WithDeferred = true;
            var r = SaveNow();
            Assert.IsTrue(r.Success, r.Message);
            Assert.AreEqual(1, a.Blocks[0].Encodes); Assert.AreEqual(1, a.Blocks[0].Disposes, "encoded once, disposed once");
            Assert.AreEqual(0, FakeDeferredBlock.Live, "success");
            var chunk = ReadChunks(paths.WorldFile(slot), out _).Single(x => x.Key == "TSTA");
            var reader = new SaveReader(chunk.Payload);
            Assert.AreEqual(7, reader.ReadInt32()); reader.ReadUInt16();
            Assert.AreEqual("deferred:TSTA", Encoding.UTF8.GetString(reader.ReadByteArray()), "the block is spliced in place");

            c.ThrowOnEncode = true;                                    // fails on the worker, after TSTA's block was encoded
            r = SaveNow();
            Assert.AreEqual(SaveErrorKind.IoError, r.Error);
            Assert.AreEqual(0, FakeDeferredBlock.Live, "encode failure");

            context.Block = SaveBlockReason.InCombat;                  // blocked: nothing captured at all
            Assert.AreEqual(SaveErrorKind.Blocked, SaveNow().Error);
            Assert.AreEqual(0, FakeDeferredBlock.Live, "blocked");
        }

        [UnityTest]
        public IEnumerator Blocked_ManualRaisesEvent_AutosaveSilent()
        {
            context.Block = SaveBlockReason.InCombat;
            var manual = service.SaveAsync(slot, SaveReason.Manual).GetAwaiter();
            while (!manual.IsCompleted) yield return null;
            Assert.AreEqual(SaveErrorKind.Blocked, manual.GetResult().Error);
            Assert.AreEqual(1, completed.Count);
            Assert.AreEqual("You can't save during combat.", completed[0].Message);

            var auto = service.SaveAsync(service.NextAutosaveSlot(slot.Profile), SaveReason.AutoTimer).GetAwaiter();
            while (!auto.IsCompleted) yield return null;
            Assert.AreEqual(SaveErrorKind.Blocked, auto.GetResult().Error);
            Assert.AreEqual(1, completed.Count, "a blocked autosave raises nothing");

            context.HasActiveSession = false;
            var none = service.SaveAsync(slot, SaveReason.Quick).GetAwaiter();
            while (!none.IsCompleted) yield return null;
            Assert.AreEqual("There is no game to save.", completed[1].Message);
            Assert.IsFalse(Directory.Exists(paths.SlotDir(slot)), "nothing written");
        }

        [UnityTest]
        public IEnumerator Queue_Rules()
        {
            a.WithDeferred = true; a.DeferredSleepMs = 300;           // keeps the first save running on the worker
            var running = service.SaveAsync(slot, SaveReason.Manual).GetAwaiter();
            Assert.IsTrue(service.IsBusy);

            var auto = service.SaveAsync(service.NextAutosaveSlot(slot.Profile), SaveReason.AutoTimer).GetAwaiter();
            Assert.IsTrue(auto.IsCompleted, "an autosave while busy is dropped at once");
            Assert.IsFalse(auto.GetResult().Success);
            Assert.AreEqual(0, completed.Count, "silently");

            a.DeferredSleepMs = 0;
            var queued = new List<UnityEngine.Awaitable<SaveResult>.Awaiter>();
            for (int i = 2; i <= 5; i++) queued.Add(service.SaveAsync(new SlotRef(slot.Profile, new SlotId("manual_00" + i)), SaveReason.Manual).GetAwaiter());
            var full = service.SaveAsync(new SlotRef(slot.Profile, new SlotId("manual_009")), SaveReason.Manual).GetAwaiter();
            Assert.IsTrue(full.IsCompleted, "the fifth waiting request is refused");
            Assert.AreEqual(SaveErrorKind.IoError, full.GetResult().Error);
            StringAssert.Contains("Too many save operations queued", completed.Single().Message);

            var quit = service.SaveAsync(new SlotRef(slot.Profile, new SlotId("quick")), SaveReason.Quit).GetAwaiter();   // jumps the queue
            var order = new List<string>();
            service.OperationCompleted += r => order.Add(r.Slot.Slot.Value);
            float until = Time.realtimeSinceStartup + 20f;
            while ((!running.IsCompleted || !quit.IsCompleted || queued.Any(q => !q.IsCompleted)) && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(running.GetResult().Success && quit.GetResult().Success && queued.All(q => q.GetResult().Success));
            CollectionAssert.AreEqual(new[] { "manual_001", "quick", "manual_002", "manual_003", "manual_004", "manual_005" }, order, "FIFO, with Quit right behind the running save");
            Assert.IsFalse(service.IsBusy);
        }

        [Test]
        public void Thumbnails_WrittenDeletedAndCached()
        {
            context.SlotThumbnail = FakeSaveContext.Image("slot", 10);
            context.Changed.Add(FakeSaveContext.Image("ship_1", 20));
            context.Changed.Add(FakeSaveContext.Image("ship_2", 30));
            context.Keep.UnionWith(new[] { "ship_1", "ship_2" });
            Assert.IsTrue(SaveNow().Success);
            string thumbs = paths.ThumbsDir(slot);
            CollectionAssert.AreEquivalent(new[] { "slot.png", "ship_1.png", "ship_2.png" }, Directory.GetFiles(thumbs).Select(Path.GetFileName));
            Assert.AreEqual("thumbs/slot.png", new NewtonsoftSaveJsonCodec().Deserialize<SaveMeta>(File.ReadAllBytes(paths.MetaFile(slot))).Thumbnail);
            Assert.IsTrue(service.TryGetSessionThumbnail("ship_1", out var png1));
            Assert.IsTrue(service.TryLoadThumbnail(slot, "ship_1", out var tex));
            Assert.AreEqual(8, tex.width); UnityEngine.Object.DestroyImmediate(tex);

            var old = DateTime.UtcNow.AddHours(-1);
            File.SetLastWriteTimeUtc(paths.ThumbnailFile(slot, "ship_1"), old);
            context.Changed.Add(FakeSaveContext.Image("ship_2", 99));  // ship_2 changes, ship_1 does not, ship_3 never existed
            context.Keep.Remove("ship_1"); context.Keep.Add("ship_1");
            context.Keep.Remove("ship_2"); context.Keep.Add("ship_2");
            Assert.IsTrue(SaveNow().Success);
            Assert.AreEqual(old, File.GetLastWriteTimeUtc(paths.ThumbnailFile(slot, "ship_1")), "unchanged thumbnail not rewritten");
            Assert.IsTrue(service.TryGetSessionThumbnail("ship_2", out var png2));
            CollectionAssert.AreEqual(png2, File.ReadAllBytes(paths.ThumbnailFile(slot, "ship_2")), "changed thumbnail rewritten");

            context.Keep.Remove("ship_2");                            // ship_2 no longer needed; no slot thumbnail this time
            context.SlotThumbnail = null;
            Assert.IsTrue(SaveNow().Success);
            CollectionAssert.AreEquivalent(new[] { "ship_1.png" }, Directory.GetFiles(thumbs).Select(Path.GetFileName));
            Assert.IsFalse(service.TryGetSessionThumbnail("ship_2", out _));
            Assert.IsNull(new NewtonsoftSaveJsonCodec().Deserialize<SaveMeta>(File.ReadAllBytes(paths.MetaFile(slot))).Thumbnail);

            var otherSlot = new SlotRef(slot.Profile, new SlotId("quick"));   // the cache carries thumbnails to a new slot
            Assert.IsTrue(service.WriteCaptured(service.CaptureNow(otherSlot, SaveReason.Quick, null)).Success);
            Assert.IsTrue(File.Exists(paths.ThumbnailFile(otherSlot, "ship_1")));
        }

        [Test]
        public void Register_Rules()
        {
            Assert.Throws<InvalidOperationException>(() => service.Register(new FakeParticipant("TSTA")), "duplicate");
            Assert.Throws<InvalidOperationException>(() => service.Register(new FakeParticipant("ZZZZ")), "not in the registry");
            Assert.Throws<InvalidOperationException>(() => service.Register(new FakeParticipant("META")), "container key");
        }

        [Test]
        public void ResetForNewGame_ClearsSessionState()
        {
            context.Changed.Add(FakeSaveContext.Image("ship_1", 1)); context.Keep.Add("ship_1");
            Assert.IsTrue(SaveNow().Success);
            service.ResetForNewGame();
            Assert.IsFalse(service.HasCurrentSlot);
            Assert.IsFalse(service.TryGetSessionThumbnail("ship_1", out _));
        }
        [Test]
        public void Preparation_PrecedesEveryParticipant()
        {
            context.OnPrepare = () => a.Value = 12345;
            var captured = service.CaptureNow(slot, SaveReason.AutoTimer, null);
            Assert.IsFalse(captured.Failure.HasValue);
            Assert.AreEqual(1, context.PreparationCalls);
            Assert.AreEqual(12345, new SaveReader(captured.Participants[0].Writer.ToArray()).ReadInt32());
            captured.Release();
            context.Block = SaveBlockReason.InCombat;
            var blocked = service.CaptureNow(slot, SaveReason.AutoTimer, null);
            Assert.IsTrue(blocked.Failure.HasValue); Assert.AreEqual(1, context.PreparationCalls, "blocked saves do not write back");
        }

        [UnityTest]
        public IEnumerator PendingThumbnails_FinishOrDropBeforeWorker()
        {
            foreach (bool finishes in new[] { true, false })
            {
                var pending = new ThumbnailRequest { Key = "slot", Width = 8, Height = 4, Pending = true };
                context.SlotThumbnail = pending;
                var captured = service.CaptureNow(slot, SaveReason.Manual, null);
                var wait = service.FinishPendingThumbnailsAsync(captured).GetAwaiter();
                if (finishes) { pending.Rgba32 = FakeSaveContext.Image("slot", 42).Rgba32; pending.Pending = false; }
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 5;
                while (!wait.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.IsTrue(wait.IsCompleted); wait.GetResult();
                Assert.AreEqual(finishes, captured.SlotThumbnail != null);
                Assert.AreEqual(finishes ? "thumbs/slot.png" : null, captured.Meta.Thumbnail);
                // A late producer can finish safely, but the worker must never regain a dropped request.
                if (!finishes) { pending.Rgba32 = FakeSaveContext.Image("slot", 99).Rgba32; pending.Pending = false; }
                Assert.AreEqual(finishes, captured.SlotThumbnail != null);
                Assert.IsTrue(service.WriteCaptured(captured).Success);
                Assert.AreEqual(finishes, File.Exists(Path.Combine(paths.SlotDir(slot), "thumbs", "slot.png")));
            }
        }
    }
}
