using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Core.Save;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>SPEC-05 §5.10: immutable version fixtures and real-service test wiring.</summary>
    public static class SaveFixtureHarness
    {
        public const string FixtureDirectory = "Assets/Game/Tests/EditMode/Persistence/Fixtures";
        public static string PathFor(string kind) => Path.Combine(FixtureDirectory, "container_v1_" + kind + ".sav.bytes");
        public static SaveService CreateService(string tempRoot, ContentService content, SaveChunkRegistry registry)
        {
            var service = new SaveService(new SavePaths(tempRoot), content, new FakePause(), new SaveServiceConfig(), registry);
            service.AttachContext(new FakeSaveContext()); return service;
        }
        public static void WriteFixture(string path, string key, int version, Action<SaveWriter> write)
        {
            if (version < 1 || version > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(version));
            var palette = SaveIdPalette.CreateEmpty(); var codec = new NewtonsoftSaveJsonCodec();
            using var writer = new SaveWriter(palette, codec); write(writer);
            var chunk = new RawChunk { Key = key, Version = (ushort)version, Payload = writer.ToArray() };
            WriteNew(path, Container(palette, new[] { chunk }));
        }
        public static SaveReader OpenChunk(string path, string key, out int version)
        {
            var chunks = ReadChunks(File.ReadAllBytes(path));
            var chunk = chunks.SingleOrDefault(c => c.Key == key) ?? throw new SaveFormatException("missing fixture chunk " + key, 0);
            version = chunk.Version;
            var palette = SaveIdPalette.Parse(chunks.Single(c => c.Key == "IDPL").Payload);
            return new SaveReader(chunk.Payload, palette, new NewtonsoftSaveJsonCodec());
        }
        public static List<RawChunk> ReadChunks(byte[] bytes)
        {
            var chunks = new List<RawChunk>(); var report = new ValidationReport("fixture");
            if (SaveContainerReader.Read(bytes, out _, chunks, report, "fixture") != ContainerReadStatus.Ok)
                throw new SaveFormatException(report.ToText(), 0);
            return chunks;
        }
        static byte[] Container(SaveIdPalette palette, IEnumerable<RawChunk> participants)
        {
            var time = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
            var meta = new SaveMeta { FormatVersion = 1, GameVersion = "fixture-v1", ProfileId = "p001", SlotId = "manual_001", SavedUtc = time, PlaytimeSeconds = 42.5, DatabaseHash = "0000000000000000" };
            var chunks = new List<RawChunk> {
                new RawChunk { Key = "META", Version = 1, Payload = new NewtonsoftSaveJsonCodec().Serialize(meta) },
                new RawChunk { Key = "IDPL", Version = 1, Payload = palette.Serialize() } };
            chunks.AddRange(participants);
            return SaveContainerWriter.Write(new ContainerHeader { SavedUtcTicks = time.Ticks, PlaytimeSeconds = 42.5 }, chunks, 64);
        }
        public static byte[] CurrentContainer(string kind)
        {
            if (kind != "minimal" && kind != "unknown_chunk" && kind != "missing_ids") throw new ArgumentException("fixture kind", nameof(kind));
            bool missing = kind == "missing_ids";
            var palette = SaveIdPalette.CreateEmpty();
            using var a = new SaveWriter(palette);
            a.WriteInt32(17);
            a.WriteContentId(new ContentId(missing ? "item_removed_for_test" : "item_missing"));
            a.WriteContentId(new ContentId(missing ? "block_removed_for_test" : "block_missing"));
            using var b = new SaveWriter(palette); b.WriteString("container fixture");
            var chunks = new List<RawChunk> {
                new RawChunk { Key = "TSTA", Version = 1, Payload = a.ToArray() },
                new RawChunk { Key = "TSTB", Version = 1, Payload = b.ToArray() } };
            if (kind == "unknown_chunk") chunks.Add(new RawChunk { Key = "ZZZZ", Version = 7, Payload = new byte[] { 1, 2, 3, 4, 255 } });
            return Container(palette, chunks);
        }
        public static void WriteContainerFixtures()
        {
            foreach (string kind in new[] { "minimal", "unknown_chunk", "missing_ids" })
            {
                string path = PathFor(kind);
                if (!File.Exists(path)) WriteNew(path, CurrentContainer(kind));
            }
        }
        static void WriteNew(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            // CreateNew is deliberate: a released version fixture must never be regenerated.
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
