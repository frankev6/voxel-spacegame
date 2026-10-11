using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core.Save;
using Game.Data.Validation;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-003: container round trip, determinism, corruption classes, the comparer and the ListSlots META fallback
    /// (SPEC-05 §4.2, §5.2, §5.5).</summary>
    public sealed class SaveContainerTests
    {
        internal static readonly ContainerHeader Header = new ContainerHeader { DatabaseHash32 = 0xDEADBEEF, SavedUtcTicks = 638_000_000_000_000_000, PlaytimeSeconds = 1234.5 };

        internal static RawChunk Chunk(string key, byte[] payload, ushort version = 1) => new RawChunk { Key = key, Version = version, Payload = payload };

        internal static List<RawChunk> Sample(int seed = 1)
        {
            var rng = new Random(seed);
            var random = new byte[5000]; rng.NextBytes(random);
            var text = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("block_hull_light;", 300)));
            return new List<RawChunk>
            {
                Chunk("META", Encoding.UTF8.GetBytes("{\"FormatVersion\":1,\"DisplayName\":\"Test\"}")),
                Chunk("IDPL", new byte[] { 2, 3, (byte)'a', (byte)'b', (byte)'c', 1, (byte)'z' }),
                Chunk("SESS", text, 3),
                Chunk("RAND", random, 2),
                Chunk("EMPT", new byte[0]),
            };
        }

        static (ContainerReadStatus status, ContainerHeader header, List<RawChunk> chunks, ValidationReport report) ReadBack(byte[] file)
        {
            var chunks = new List<RawChunk>();
            var report = new ValidationReport("test");
            var status = SaveContainerReader.Read(file, out var header, chunks, report, "world.sav");
            return (status, header, chunks, report);
        }

        static byte[] Pattern(int length)
        {
            var b = new byte[length];
            for (int i = 0; i < length; i++) b[i] = (byte)(i % 251);
            return b;
        }

        [Test]
        public void RoundTrip_PayloadSizes()
        {
            var rng = new Random(3);
            var incompressible = new byte[4096]; rng.NextBytes(incompressible);
            var cases = new (string key, byte[] payload, bool expectDeflate)[]
            {
                ("ZERO", new byte[0], false),
                ("B063", Pattern(63), false),                       // below the 64-byte threshold
                ("B064", new byte[64], true),                       // 64 zeros deflate to less
                ("RAND", incompressible, false),                    // deflate would not be smaller
                ("BIG1", Pattern(10 * 1024 * 1024), true),
            };
            var chunks = new List<RawChunk> { Chunk("META", Encoding.UTF8.GetBytes("{}")), Chunk("IDPL", new byte[] { 0 }) };
            foreach (var c in cases) chunks.Add(Chunk(c.key, c.payload, 7));
            var file = SaveContainerWriter.Write(Header, chunks, 64);

            var (status, header, read, report) = ReadBack(file);
            Assert.AreEqual(ContainerReadStatus.Ok, status, string.Join("; ", report.Entries.Select(e => e.Message)));
            Assert.AreEqual(Header.DatabaseHash32, header.DatabaseHash32);
            Assert.AreEqual(Header.SavedUtcTicks, header.SavedUtcTicks);
            Assert.AreEqual(Header.PlaytimeSeconds, header.PlaytimeSeconds);
            Assert.AreEqual(1, header.FormatVersion);
            Assert.AreEqual((uint)chunks.Count, header.ChunkCount);
            CollectionAssert.AreEqual(chunks.Select(c => c.Key), read.Select(c => c.Key));
            for (int i = 0; i < chunks.Count; i++)
            {
                Assert.IsFalse(read[i].Corrupt, read[i].Key);
                Assert.AreEqual(chunks[i].Version, read[i].Version, read[i].Key);
                Assert.IsTrue(chunks[i].Payload.AsSpan().SequenceEqual(read[i].Payload), read[i].Key);
            }
            foreach (var c in cases)
                Assert.AreEqual(c.expectDeflate, (read.Single(r => r.Key == c.key).StoredFlags & ChunkFlags.Deflate) != 0, $"{c.key} deflate flag");
            Assert.Less(file.Length, 1024 * 1024, "the 10 MB pattern deflates");
        }

        [Test]
        public void Write_IsDeterministic()
        {
            var a = SaveContainerWriter.Write(Header, Sample(), 64);
            var b = SaveContainerWriter.Write(Header, Sample(), 64);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Write_RejectsBadInput()
        {
            Assert.Throws<ArgumentException>(() => SaveContainerWriter.Write(Header, new List<RawChunk> { Chunk("META", new byte[0]) }, 64), "fewer than 2 chunks");
            Assert.Throws<ArgumentException>(() => SaveContainerWriter.Write(Header, new List<RawChunk> { Chunk("META", new byte[0]), Chunk("META", new byte[0]) }, 64), "duplicate");
            Assert.Throws<ArgumentException>(() => SaveContainerWriter.Write(Header, new List<RawChunk> { Chunk("META", new byte[0]), Chunk("idpl", new byte[0]) }, 64), "lower case");
            Assert.Throws<ArgumentException>(() => SaveContainerWriter.Write(Header, new List<RawChunk> { Chunk("META", new byte[0]), Chunk("ID", new byte[0]) }, 64), "length");
            Assert.Throws<ArgumentException>(() => SaveContainerWriter.Write(Header, new List<RawChunk> { Chunk("META", new byte[0]), Chunk("IDPL", new byte[0], 0) }, 64), "version 0");
        }

        static int PayloadOffset(byte[] file, string key)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(28));
            int offset = 36 + 20 * (int)count;
            for (int i = 0; i < count; i++)
            {
                int e = 36 + 20 * i;
                if (Encoding.ASCII.GetString(file, e, 4) == key) return offset;
                offset += (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(e + 8));
            }
            throw new ArgumentException(key);
        }

        internal static void FixHeaderCrc(byte[] file)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(28));
            uint crc = Crc32.Append(Crc32.Compute(file.AsSpan(0, 32)), file.AsSpan(36, 20 * (int)count));
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(32), crc);
        }

        [Test]
        public void Read_ClassifiesCorruption()
        {
            var good = SaveContainerWriter.Write(Header, Sample(), 64);
            Assert.AreEqual(ContainerReadStatus.Ok, ReadBack(good).status);

            var badMagic = (byte[])good.Clone(); badMagic[0] = (byte)'X';
            Assert.AreEqual(ContainerReadStatus.NotAContainer, ReadBack(badMagic).status);
            Assert.AreEqual(ContainerReadStatus.NotAContainer, ReadBack(new byte[10]).status, "shorter than a header");

            var v2 = (byte[])good.Clone(); v2[4] = 2;
            Assert.AreEqual(ContainerReadStatus.NewerFormat, ReadBack(v2).status);
            var v0 = (byte[])good.Clone(); v0[4] = 0;
            Assert.AreEqual(ContainerReadStatus.NotAContainer, ReadBack(v0).status);

            var header = (byte[])good.Clone(); header[9] ^= 0x10;             // databaseHash32
            Assert.AreEqual(ContainerReadStatus.CorruptHeader, ReadBack(header).status);
            var table = (byte[])good.Clone(); table[36 + 20 + 9] ^= 0x01;    // IDPL storedLength
            Assert.AreEqual(ContainerReadStatus.CorruptHeader, ReadBack(table).status);

            var payload = (byte[])good.Clone(); payload[PayloadOffset(good, "RAND") + 100] ^= 0xFF;
            var (status, _, chunks, report) = ReadBack(payload);
            Assert.AreEqual(ContainerReadStatus.CorruptChunks, status);
            CollectionAssert.AreEqual(new[] { "RAND" }, chunks.Where(c => c.Corrupt).Select(c => c.Key), "only that chunk is corrupt");
            Assert.IsNull(chunks.Single(c => c.Key == "RAND").Payload);
            Assert.AreEqual("PERS-E011", report.Entries.Single().Code);
            Assert.IsTrue(chunks.Where(c => !c.Corrupt).All(c => c.Payload != null));

            var deflated = (byte[])good.Clone(); deflated[PayloadOffset(good, "SESS") + 3] ^= 0x40;
            Assert.AreEqual(new[] { "SESS" }, ReadBack(deflated).chunks.Where(c => c.Corrupt).Select(c => c.Key).ToArray(), "a deflated chunk too");

            Assert.AreEqual(ContainerReadStatus.CorruptHeader, ReadBack(good.AsSpan(0, good.Length - 1).ToArray()).status, "truncated by one byte");
            Assert.AreEqual(ContainerReadStatus.CorruptHeader, ReadBack(good.AsSpan(0, 40).ToArray()).status, "truncated in the table");
            var longer = new byte[good.Length + 1]; good.CopyTo(longer, 0);
            Assert.AreEqual(ContainerReadStatus.CorruptHeader, ReadBack(longer).status, "trailing byte");
        }

        [Test]
        public void UnknownFlags_AreCorrupt()
        {
            var good = SaveContainerWriter.Write(Header, Sample(), 64);
            var headerFlags = (byte[])good.Clone(); headerFlags[6] = 1; FixHeaderCrc(headerFlags);
            Assert.AreEqual(ContainerReadStatus.CorruptHeader, ReadBack(headerFlags).status, "headerFlags ≠ 0 with a valid CRC");
            var chunkFlags = (byte[])good.Clone(); chunkFlags[36 + 20 * 2 + 6] |= 0x02; FixHeaderCrc(chunkFlags);
            Assert.AreEqual(ContainerReadStatus.CorruptHeader, ReadBack(chunkFlags).status, "chunk flag bit 1 with a valid CRC");
            var count = (byte[])good.Clone(); BinaryPrimitives.WriteUInt32LittleEndian(count.AsSpan(28), 1); FixHeaderCrc(count);
            Assert.AreEqual(ContainerReadStatus.CorruptHeader, ReadBack(count).status, "chunkCount < 2");
            var dup = (byte[])good.Clone(); Encoding.ASCII.GetBytes("META").CopyTo(dup, 36 + 20); FixHeaderCrc(dup);
            Assert.AreEqual(ContainerReadStatus.CorruptHeader, ReadBack(dup).status, "duplicate fourCC");
        }

        [Test]
        public void Comparer_IgnoresVolatileFields_ReportsFirstDifference()
        {
            var a = SaveContainerWriter.Write(Header, Sample(), 64);
            var later = Header; later.SavedUtcTicks += 999; later.PlaytimeSeconds += 60;
            var chunks = Sample();
            chunks[0] = Chunk("META", Encoding.UTF8.GetBytes("{\"FormatVersion\":1,\"DisplayName\":\"A different, longer name\"}"));
            var b = SaveContainerWriter.Write(later, chunks, 64);
            Assert.IsTrue(SaveFileComparer.EqualIgnoringVolatile(a, b, out var diff), diff);
            Assert.IsNull(diff);

            chunks[3].Payload[10] ^= 1;
            var c = SaveContainerWriter.Write(later, chunks, 64);
            Assert.IsFalse(SaveFileComparer.EqualIgnoringVolatile(a, c, out diff));
            StringAssert.Contains("table entry 3", diff, "the RAND entry's CRC is the first byte that differs");

            var e = (byte[])a.Clone(); e[e.Length - 1] ^= 1;          // payload byte only (table untouched): RAND's last byte, EMPT is empty
            Assert.IsFalse(SaveFileComparer.EqualIgnoringVolatile(a, e, out diff));
            StringAssert.Contains("chunk RAND payload +4999", diff);

            var d = SaveContainerWriter.Write(new ContainerHeader { DatabaseHash32 = 1 }, Sample(), 64);
            Assert.IsFalse(SaveFileComparer.EqualIgnoringVolatile(a, d, out diff));
            StringAssert.StartsWith("offset 8", diff);
        }

        [Test]
        public void ListSlots_ReadsMetaChunk_WhenMetaJsonMissing()
        {
            var root = Path.Combine(Path.GetTempPath(), "SaveContainerTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                var paths = new SavePaths(root);
                var slot = new SlotRef(new ProfileId("p001"), new SlotId("manual_001"));
                Directory.CreateDirectory(paths.SlotDir(slot));
                var json = new NewtonsoftSaveJsonCodec();
                var meta = new SaveMeta { FormatVersion = 1, ProfileId = "p001", SlotId = "manual_001", DisplayName = "From the container", Reason = SaveReason.Manual, SavedUtc = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc) };
                var chunks = Sample();
                chunks[0] = Chunk("META", json.Serialize(meta));
                var big = new byte[200_000]; new Random(5).NextBytes(big);
                chunks.Add(Chunk("HUGE", big));                      // the file is larger than the 64 KB ListSlots reads
                File.WriteAllBytes(paths.WorldFile(slot), SaveContainerWriter.Write(Header, chunks, 64));

                var info = new SaveService(paths, null, null, new SaveServiceConfig()).ListSlots(new ProfileId("p001")).Single();
                Assert.AreEqual(SlotStatus.MetaMissing, info.Status);
                Assert.IsNotNull(info.Meta, "META read from the first 64 KB of world.sav");
                Assert.AreEqual("From the container", info.Meta.DisplayName);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
