using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Core.Events;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-006: never-reject loading (SPEC-05 decision 4, §5.6, §7) with the exact report codes.</summary>
    public sealed class LoadPipelineTests
    {
        static readonly SaveChunkRegistry TestRegistry = new SaveChunkRegistry(new[]
        {
            new ChunkRegistration("TSTA", 1000, "test", "a"), new ChunkRegistration("TSTB", 1010, "test", "b"), new ChunkRegistration("TSTC", 1020, "test", "c"),
        });

        string root;
        SavePaths paths;
        readonly SlotRef slot = new SlotRef(new ProfileId("p001"), new SlotId("manual_001"));
        readonly NewtonsoftSaveJsonCodec json = new NewtonsoftSaveJsonCodec();
        readonly List<SaveService> services = new List<SaveService>();
        ContentService content;

        [SetUp]
        public void SetUp()
        {
            EventHub.Reset();
            FakeParticipant.RestoreLog.Clear();
            root = Path.Combine(Path.GetTempPath(), "LoadPipelineTests_" + Guid.NewGuid().ToString("N"));
            paths = new SavePaths(root);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            foreach (var s in services) s.Dispose();
            services.Clear();
            content?.Dispose(); content = null;
            DestroyAll();
            EventHub.Reset();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        SaveService Service(ContentService withContent, params ISaveParticipant[] participants)
        {
            var s = new SaveService(paths, withContent, null, new SaveServiceConfig(), TestRegistry);
            s.AttachContext(new FakeSaveContext());
            foreach (var p in participants) s.Register(p);
            services.Add(s);
            return s;
        }

        (FakeParticipant a, FakeParticipant b, FakeParticipant c) Fakes() => (new FakeParticipant("TSTA", 3), new FakeParticipant("TSTB", 2), new FakeParticipant("TSTC"));

        void Save(SaveService s) { var r = s.WriteCaptured(s.CaptureNow(slot, SaveReason.Manual, null)); Assert.IsTrue(r.Success, r.Message); }

        LoadedSave Read(SaveService s) { var r = s.ReadNow(slot); Assert.IsTrue(r.Success, r.Message); return r.Save; }

        static string[] Codes(ValidationReport report) => report.Entries.Select(e => e.Code).ToArray();

        void WriteContainer(string file, params RawChunk[] chunks)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, SaveContainerWriter.Write(new ContainerHeader(), chunks, 64));
        }

        RawChunk Meta() => new RawChunk { Key = "META", Version = 1, Payload = json.Serialize(new SaveMeta { FormatVersion = 1, ProfileId = "p001", SlotId = "manual_001" }) };

        static RawChunk Idpl(params string[] ids)
        {
            var p = SaveIdPalette.CreateEmpty();
            foreach (var id in ids) p.GetOrAdd(new ContentId(id));
            return new RawChunk { Key = "IDPL", Version = 1, Payload = p.Serialize() };
        }

        static byte[] FakePayload(int value, ushort contentIndex)
        {
            using var w = new SaveWriter();
            w.WriteInt32(value); w.WriteUInt16(contentIndex); w.WriteString("end");
            return w.ToArray();
        }

        static void Corrupt(string file, int offsetFromEnd)
        {
            var bytes = File.ReadAllBytes(file);
            bytes[bytes.Length - offsetFromEnd] ^= 0x5A;
            File.WriteAllBytes(file, bytes);
        }

        [Test]
        public void Restore_OrderFollowsRegistry()
        {
            var (a, b, c) = Fakes();
            a.Value = 11; b.Value = 22; c.Value = 33;
            Save(Service(null, c, a, b));
            var (a2, b2, c2) = Fakes();
            var loader = Service(null, b2, c2, a2);                    // registered in yet another order
            FakeParticipant.RestoreLog.Clear();
            var report = loader.Restore(Read(loader));
            CollectionAssert.AreEqual(new[] { "TSTA@3", "TSTB@2", "TSTC@1" }, FakeParticipant.RestoreLog);
            Assert.AreEqual(11, a2.RestoredValue); Assert.AreEqual(22, b2.RestoredValue); Assert.AreEqual(33, c2.RestoredValue);
            Assert.AreEqual("block_tstb", b2.RestoredContent);
            CollectionAssert.IsEmpty(Codes(report), "a clean load reports nothing");
            Assert.AreEqual(slot, loader.CurrentSlot); Assert.AreSame(report, loader.LastLoadReport);
        }

        [Test]
        public void MissingChunk_ResetsParticipant()
        {
            var (a, b, c) = Fakes();
            Save(Service(null, a, c));                                  // no TSTB in the file
            var (a2, b2, c2) = Fakes();
            var loader = Service(null, a2, b2, c2);
            var report = loader.Restore(Read(loader));
            Assert.AreEqual(0, b2.RestoredFromVersion); Assert.AreEqual(0, b2.RestoredValue);
            CollectionAssert.AreEqual(new[] { "PERS-I020" }, Codes(report));
        }

        [Test]
        public void OlderChunk_RestoresWithItsVersion()
        {
            var old = new FakeParticipant("TSTA", 1) { Value = 5 };
            Save(Service(null, old));
            var current = new FakeParticipant("TSTA", 3);
            var loader = Service(null, current);
            loader.Restore(Read(loader));
            Assert.AreEqual(1, current.RestoredFromVersion, "participant upgrades on read");
            Assert.AreEqual(5, current.RestoredValue);
        }

        [Test]
        public void NewerChunk_PreservedAndReset()
        {
            var newer = FakePayload(77, 0);
            WriteContainer(paths.WorldFile(slot), Meta(), Idpl("block_future"), new RawChunk { Key = "TSTA", Version = 9, Payload = newer });
            var a = new FakeParticipant("TSTA", 3);
            var loader = Service(null, a);
            var report = loader.Restore(Read(loader));
            Assert.AreEqual(0, a.RestoredFromVersion, "reset to defaults");
            CollectionAssert.AreEqual(new[] { "PERS-W021" }, Codes(report));

            Save(loader);                                              // the newer chunk is written back instead of the reset state
            var chunks = new List<RawChunk>();
            SaveContainerReader.Read(File.ReadAllBytes(paths.WorldFile(slot)), out _, chunks, new ValidationReport("t"), "t");
            var tsta = chunks.Single(x => x.Key == "TSTA");
            Assert.AreEqual(9, tsta.Version);
            CollectionAssert.AreEqual(newer, tsta.Payload);
        }

        [Test]
        public void ThrowingParticipant_IsIsolated()
        {
            LogAssert.ignoreFailingMessages = true;                   // Restore logs its report, errors included (reset per test)
            var (a, b, c) = Fakes();
            Save(Service(null, a, b, c));
            var (a2, b2, c2) = Fakes();
            b2.ThrowOnRestore = true;
            c2.ThrowOnRestore = true; c2.ThrowOnDefaultRestore = true;
            var loader = Service(null, a2, b2, c2);
            var report = loader.Restore(Read(loader));
            Assert.AreEqual(7, a2.RestoredValue, "others unaffected");
            Assert.AreEqual(0, b2.RestoredFromVersion, "the thrower is reset");
            CollectionAssert.AreEqual(new[] { "PERS-E022", "PERS-E022", "PERS-E023" }, Codes(report));
        }

        [Test]
        public void UnreadBytes_Warn()
        {
            var a = new FakeParticipant("TSTA");
            Save(Service(null, a));
            var a2 = new FakeParticipant("TSTA") { ReadAll = false };
            var loader = Service(null, a2);
            CollectionAssert.AreEqual(new[] { "PERS-W024" }, Codes(loader.Restore(Read(loader))));
        }

        [Test]
        public void UnknownChunk_PreservedAcrossResave()
        {
            // ZZZZ (from a newer build) refers to palette index 1 = "block_y".
            var zzzz = new RawChunk { Key = "ZZZZ", Version = 4, Payload = new byte[] { 1, 0, 0xAB, 0xCD } };
            WriteContainer(paths.WorldFile(slot), Meta(), Idpl("block_x", "block_y"), new RawChunk { Key = "TSTA", Version = 3, Payload = FakePayload(9, 0) }, zzzz);
            var a = new FakeParticipant("TSTA", 3);
            var loader = Service(null, a);
            var report = loader.Restore(Read(loader));
            CollectionAssert.AreEqual(new[] { "PERS-I025" }, Codes(report));
            Assert.AreEqual("block_x", a.RestoredContent);

            Save(loader);
            var chunks = new List<RawChunk>();
            SaveContainerReader.Read(File.ReadAllBytes(paths.WorldFile(slot)), out _, chunks, new ValidationReport("t"), "t");
            var kept = chunks.Single(x => x.Key == "ZZZZ");
            Assert.AreEqual(4, kept.Version);
            CollectionAssert.AreEqual(zzzz.Payload, kept.Payload, "byte-for-byte");
            var palette = SaveIdPalette.Parse(chunks.Single(x => x.Key == "IDPL").Payload);
            Assert.AreEqual("block_y", palette.Get(1).Value, "its palette references still map to the same strings");
            Assert.AreEqual("block_x", palette.Get(0).Value);

            loader.ResetForNewGame();                                  // a new game drops preserved chunks
            Save(loader);
            chunks.Clear();
            SaveContainerReader.Read(File.ReadAllBytes(paths.WorldFile(slot)), out _, chunks, new ValidationReport("t"), "t");
            Assert.IsFalse(chunks.Any(x => x.Key == "ZZZZ"));
        }

        [Test]
        public void CorruptSav_FallsBackToBak()
        {
            var (a, b, c) = Fakes();
            var saver = Service(null, a, b, c);
            a.Value = 1; Save(saver);
            a.Value = 2; Save(saver);
            Corrupt(paths.WorldFile(slot), 5);                         // a payload byte of the newest file
            var (a2, b2, c2) = Fakes();
            var loader = Service(null, a2, b2, c2);
            var save = Read(loader);
            Assert.IsTrue(save.FromBackup);
            var report = loader.Restore(save);
            Assert.AreEqual(1, a2.RestoredValue, "one save older");
            CollectionAssert.AreEqual(new[] { "PERS-W010" }, Codes(report));
        }

        [Test]
        public void BothCorrupt_UsesValidChunks()
        {
            LogAssert.ignoreFailingMessages = true;                   // Restore logs its report, errors included (reset per test)
            var (a, b, c) = Fakes();
            var saver = Service(null, a, b, c);
            Save(saver); Save(saver);
            Corrupt(paths.WorldFile(slot), 5);                         // TSTC's payload, the last chunk
            Corrupt(paths.BackupFile(slot), 5);
            var (a2, b2, c2) = Fakes();
            var loader = Service(null, a2, b2, c2);
            var save = Read(loader);
            Assert.IsFalse(save.FromBackup);
            var report = loader.Restore(save);
            Assert.AreEqual(7, a2.RestoredValue);
            Assert.AreEqual(0, c2.RestoredFromVersion, "the corrupt chunk's participant is reset");
            CollectionAssert.AreEqual(new[] { "PERS-E011" }, Codes(report));
        }

        [UnityTest]
        public IEnumerator BothHeadersCorrupt_FailsUnreadable()
        {
            var (a, b, c) = Fakes();
            var saver = Service(null, a, b, c);
            Save(saver); Save(saver);
            foreach (var f in new[] { paths.WorldFile(slot), paths.BackupFile(slot) }) { var bytes = File.ReadAllBytes(f); bytes[9] ^= 1; File.WriteAllBytes(f, bytes); }
            var (a2, b2, c2) = Fakes();
            var loader = Service(null, a2, b2, c2);
            var completed = new List<SaveOperationResult>();
            loader.OperationCompleted += r => completed.Add(r);
            var op = loader.ReadAsync(slot).GetAwaiter();
            while (!op.IsCompleted) yield return null;
            Assert.AreEqual(SaveErrorKind.Unreadable, op.GetResult().Error);
            Assert.AreEqual("This save file is damaged and cannot be loaded.", completed.Single().Message);
            Assert.IsTrue(completed[0].IsLoad);
            Assert.AreEqual(0, a2.RestoreCalls + b2.RestoreCalls + c2.RestoreCalls);
        }

        [Test]
        public void NewerFormat_Fails()
        {
            Save(Service(null, Fakes().a));
            var bytes = File.ReadAllBytes(paths.WorldFile(slot)); bytes[4] = 2; File.WriteAllBytes(paths.WorldFile(slot), bytes);
            var r = Service(null).ReadNow(slot);
            Assert.AreEqual(SaveErrorKind.NewerFormat, r.Error);
            Assert.AreEqual("This save was made by a newer version of the game.", r.Message);
        }

        [Test]
        public void MetaAndPaletteDamage_AreReported()
        {
            LogAssert.ignoreFailingMessages = true;                   // Restore logs its report, errors included (reset per test)
            WriteContainer(paths.WorldFile(slot), new RawChunk { Key = "META", Version = 1, Payload = new byte[] { (byte)'{' } },
                           new RawChunk { Key = "IDPL", Version = 1, Payload = new byte[] { 5 } },
                           new RawChunk { Key = "TSTA", Version = 3, Payload = FakePayload(4, 0) });
            var a = new FakeParticipant("TSTA", 3);
            var loader = Service(null, a);
            var save = Read(loader);
            Assert.IsNull(save.Meta);
            var report = loader.Restore(save);
            CollectionAssert.AreEquivalent(new[] { "PERS-W013", "PERS-E012", "PERS-E022" }, Codes(report), "an empty palette makes TSTA's content id unreadable");
            Assert.AreEqual(0, a.RestoredFromVersion);
        }

        sealed class ResolvingParticipant : ISaveParticipant
        {
            readonly ContentCatalog catalog;
            public Game.Data.Definitions.BlockDefinition Resolved;
            public ResolvingParticipant(ContentCatalog catalog) { this.catalog = catalog; }
            public string Key => "TSTC";
            public int Version => 1;
            public void Capture(SaveWriter w) { w.WriteContentId(new ContentId("block_removed_since")); }
            public void Restore(SaveReader r, int fromVersion) { if (fromVersion > 0) Resolved = catalog.ResolveBlockOrMissing(r.ReadContentId(), "fleet ship 1"); }
        }

        [Test]
        public void UnknownContentId_WarnsAndUsesPlaceholder()
        {
            Save(Service(null, new ResolvingParticipant(null)));       // saved without content: databaseHash32 = 0
            content = ContentService.CreateForTests(new ContentDefinition[] { Block("block_missing", Game.Data.Blocks.BlockKind.Structural), Item("item_missing") });
            var participant = new ResolvingParticipant(content.Catalog);
            var loader = Service(content);
            loader.Register(participant);
            var completed = new List<SaveOperationResult>();
            loader.OperationCompleted += r => completed.Add(r);
            var report = loader.Restore(Read(loader));
            Assert.AreSame(content.Catalog.MissingBlock, participant.Resolved, "block_missing placeholder");
            CollectionAssert.AreEquivalent(new[] { "PERS-W001", "PERS-I002" }, Codes(report));
            StringAssert.Contains("block_removed_since", report.Entries.Single(e => e.Code == "PERS-W001").Message);
            Assert.GreaterOrEqual(completed.Single().WarningCount, 1);
            Assert.AreEqual("Game loaded with 1 warnings.", completed[0].Message);
        }

        [Test]
        public void FailedRead_DoesNotTouchParticipants()
        {
            var (a, b, c) = Fakes();
            var loader = Service(null, a, b, c);
            var r = loader.ReadNow(slot);
            Assert.AreEqual(SaveErrorKind.NotFound, r.Error);
            Assert.AreEqual("This save could not be found.", r.Message);
            Assert.AreEqual(0, a.RestoreCalls + b.RestoreCalls + c.RestoreCalls);
            Assert.IsFalse(loader.HasCurrentSlot);
        }

        [Test]
        public void RestoreTwice_Throws()
        {
            var (a, b, c) = Fakes();
            Save(Service(null, a));
            var loader = Service(null, new FakeParticipant("TSTA", 3));
            var save = Read(loader);
            loader.Restore(save);
            Assert.Throws<InvalidOperationException>(() => loader.Restore(save));
        }

        [Test]
        public void Restore_PublishesGameLoaded()
        {
            var loaded = new List<GameLoaded>();
            Action<GameLoaded> on = e => loaded.Add(e);
            EventBus<GameLoaded>.Subscribe(on);
            try
            {
                Save(Service(null, Fakes().a));
                var loader = Service(null, new FakeParticipant("TSTA", 3));
                loader.Restore(Read(loader));
                EventHub.DrainAll();
                Assert.AreEqual(1, loaded.Count); Assert.AreEqual("manual_001", loaded[0].SlotId); Assert.AreEqual(0, loaded[0].WarningCount);
            }
            finally { EventBus<GameLoaded>.Unsubscribe(on); }
        }
    }
}
