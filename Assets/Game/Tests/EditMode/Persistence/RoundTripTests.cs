using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Core.Events;
using Game.Core.Save;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>SPEC-05 PERS-011: fresh participants restore actual bytes before capture into another slot.</summary>
    public sealed class RoundTripTests
    {
        public sealed class JsonState
        {
            public string Name;
            public int[] Values;
            public double Position;
        }
        sealed class JsonParticipant : ISaveParticipant
        {
            public string Key => "TSTB";
            public int Version => 1;
            public JsonState State;
            public void Capture(SaveWriter w) => w.WriteJson(State);
            public void Restore(SaveReader r, int version) => State = version == 0 ? null : r.ReadJson<JsonState>();
        }
        sealed class BinaryParticipant : ISaveParticipant
        {
            public string Key => "TSTA";
            public int Version => 1;
            public int Value;
            public ContentId Id;
            public void Capture(SaveWriter w) { w.WriteInt32(Value); w.WriteContentId(Id); }
            public void Restore(SaveReader r, int version)
            {
                if (version == 0) { Value = 0; Id = default; return; }
                Value = r.ReadInt32(); Id = r.ReadContentId();
            }
        }
        sealed class StructureParticipant : ISaveParticipant, IDisposable
        {
            public string Key => "TSTC";
            public int Version => 1;
            readonly FakeStructureParticipant inner;
            readonly VoxsBlockNames names;
            bool loaded;
            public StructureParticipant(ContentCatalog c, params VoxsSource[] sources)
            { inner = new FakeStructureParticipant(c, sources); names = VoxsBlockNames.FromCatalog(c); }
            public int RestoredCount => inner.Restored.Count;
            public void Capture(SaveWriter w)
            {
                if (!loaded) { inner.Capture(w); return; }
                w.WriteVarUInt32((uint)inner.Restored.Count);
                foreach (var structure in inner.Restored)
                    w.WriteDeferred(VoxsSnapshotBlock.Capture(VoxsTestStructures.View(structure), names));
            }
            public void Restore(SaveReader r, int version) { inner.Restore(r, version); loaded = true; }
            public void Dispose() => inner.Dispose();
        }
        static readonly SaveChunkRegistry Registry = new SaveChunkRegistry(new[] {
            new ChunkRegistration("TSTA", 1000, "test", "binary"),
            new ChunkRegistration("TSTB", 1010, "test", "json"),
            new ChunkRegistration("TSTC", 1020, "test", "voxs") });
        string root;
        SavePaths paths;
        readonly List<SaveService> services = new List<SaveService>();
        readonly List<StructureParticipant> structures = new List<StructureParticipant>();
        static SlotRef Slot(int i) => new SlotRef(new ProfileId("p001"), new SlotId("manual_00" + i));
        [SetUp] public void SetUp()
        {
            EventHub.Reset(); root = Path.Combine(Path.GetTempPath(), "RoundTripTests_" + Guid.NewGuid().ToString("N")); paths = new SavePaths(root);
        }
        [TearDown] public void TearDown()
        {
            foreach (var s in services) s.Dispose(); services.Clear();
            foreach (var s in structures) s.Dispose(); structures.Clear();
            TestContentFactory.DestroyAll(); EventHub.Reset();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        SaveService Service(BinaryParticipant binary, JsonParticipant json, StructureParticipant vox)
        {
            var s = new SaveService(paths, null, null, new SaveServiceConfig(), Registry);
            s.AttachContext(new FakeSaveContext());
            s.Register(vox); s.Register(json); s.Register(binary);
            services.Add(s); structures.Add(vox); return s;
        }
        byte[] Save(SaveService s, int slot)
        {
            var r = s.WriteCaptured(s.CaptureNow(Slot(slot), SaveReason.Manual, "Round trip " + slot));
            Assert.IsTrue(r.Success, r.Message); return File.ReadAllBytes(paths.WorldFile(Slot(slot)));
        }
        static List<RawChunk> Chunks(byte[] bytes, out ContainerHeader header)
        {
            var chunks = new List<RawChunk>();
            Assert.AreEqual(ContainerReadStatus.Ok, SaveContainerReader.Read(bytes, out header, chunks, new ValidationReport("round trip"), "test"));
            return chunks;
        }
        (byte[] original, SaveService restored, BinaryParticipant binary) Restore(bool unknown)
        {
            var catalog = VoxsCodecTests.Catalog();
            byte[] first;
            using (var a = VoxsTestStructures.Random(catalog, 101))
            using (var b = VoxsTestStructures.Random(catalog, 102))
            {
                var source = Service(new BinaryParticipant { Value = 13579, Id = new ContentId("block_hull") },
                    new JsonParticipant { State = new JsonState { Name = "Preserve me", Values = new[] { 1, -7, 8 }, Position = -123.125 } },
                    new StructureParticipant(catalog, a.Source, b.Source));
                first = Save(source, 1);
            } // Original native arrays are gone before restore; recapturing them cannot make this test pass.
            if (unknown)
            {
                var chunks = Chunks(first, out var header);
                var palette = SaveIdPalette.Parse(chunks.Single(c => c.Key == "IDPL").Payload);
                ushort unknownId = palette.GetOrAdd(new ContentId("item_future_content"));
                chunks.Single(c => c.Key == "IDPL").Payload = palette.Serialize();
                using var writer = new SaveWriter(); writer.WriteUInt16(unknownId); writer.WriteBytes(new byte[1000]);
                chunks.Add(new RawChunk { Key = "ZZZZ", Version = 42, Payload = writer.ToArray() });
                first = SaveContainerWriter.Write(header, chunks, 64);
                File.WriteAllBytes(paths.WorldFile(Slot(1)), first);
            }
            var binary = new BinaryParticipant(); var json = new JsonParticipant(); var vox = new StructureParticipant(catalog);
            var restored = Service(binary, json, vox);
            var loaded = restored.ReadNow(Slot(1)); Assert.IsTrue(loaded.Success, loaded.Message);
            var report = restored.Restore(loaded.Save); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Assert.AreEqual(13579, binary.Value); Assert.AreEqual("block_hull", binary.Id.Value);
            Assert.AreEqual("Preserve me", json.State.Name); CollectionAssert.AreEqual(new[] { 1, -7, 8 }, json.State.Values);
            Assert.AreEqual(-123.125, json.State.Position); Assert.AreEqual(2, vox.RestoredCount);
            return (first, restored, binary);
        }
        [Test] public void SaveLoadSave_IsByteIdentical()
        {
            var state = Restore(false); byte[] second = Save(state.restored, 2);
            Assert.IsTrue(SaveFileComparer.EqualIgnoringVolatile(state.original, second, out string difference), difference);
        }
        [Test] public void WithUnknownChunk_IsByteIdentical()
        {
            var state = Restore(true); byte[] second = Save(state.restored, 2);
            Assert.IsTrue(SaveFileComparer.EqualIgnoringVolatile(state.original, second, out string difference), difference);
            var firstChunks = Chunks(state.original, out _); var secondChunks = Chunks(second, out _);
            var first = firstChunks.Single(c => c.Key == "ZZZZ"); var last = secondChunks.Single(c => c.Key == "ZZZZ");
            Assert.AreEqual(first.Version, last.Version); Assert.AreEqual(first.StoredFlags, last.StoredFlags);
            CollectionAssert.AreEqual(first.Payload, last.Payload);
            CollectionAssert.AreEqual(firstChunks.Single(c => c.Key == "IDPL").Payload, secondChunks.Single(c => c.Key == "IDPL").Payload);
        }
        [Test] public void Comparer_DetectsDifference()
        {
            var state = Restore(false); state.binary.Value++;
            byte[] changed = Save(state.restored, 2);
            Assert.IsFalse(SaveFileComparer.EqualIgnoringVolatile(state.original, changed, out string difference));
            int index = Chunks(state.original, out _).FindIndex(c => c.Key == state.binary.Key);
            StringAssert.Contains("table entry " + index, difference, "the changed binary chunk's CRC must identify its table entry");
        }
    }
}
