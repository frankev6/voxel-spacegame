using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Save;
using Game.Persistence.Container;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-004: the IDPL palette (SPEC-05 §4.3) and its use through SPEC-02's SaveWriter/SaveReader.</summary>
    public sealed class SaveIdPaletteTests
    {
        static ContentId Id(string s) => new ContentId(s);

        [Test]
        public void Indices_FollowFirstUse()
        {
            var p = SaveIdPalette.CreateEmpty();
            Assert.AreEqual(0, p.GetOrAdd(Id("block_hull")));
            Assert.AreEqual(1, p.GetOrAdd(Id("item_iron")));
            Assert.AreEqual(0, p.GetOrAdd(Id("block_hull")), "same id, same index");
            Assert.AreEqual(2, p.GetOrAdd(Id("block_thruster_m")));
            Assert.AreEqual(3, p.Count);
            Assert.AreEqual(Id("item_iron"), p.Get(1));
        }

        [Test]
        public void EmptyId_IsNone()
        {
            var p = SaveIdPalette.CreateEmpty();
            Assert.AreEqual(SaveIdPalette.None, p.GetOrAdd(ContentId.None));
            Assert.AreEqual(SaveIdPalette.None, p.GetOrAdd(Id("")));
            Assert.AreEqual(0, p.Count, "None takes no entry");
            Assert.IsTrue(p.Get(SaveIdPalette.None).IsNone);
        }

        [Test]
        public void SerializeParse_RoundTrip()
        {
            var p = SaveIdPalette.CreateEmpty();
            var ids = new[] { "block_hull", "removed_in_v2_block", "Ünïcødé_тест_航", "a", new string('x', 256) };
            foreach (var s in ids) p.GetOrAdd(Id(s));
            var bytes = p.Serialize();
            var q = SaveIdPalette.Parse(bytes);
            Assert.AreEqual(ids.Length, q.Count);
            for (ushort i = 0; i < ids.Length; i++) Assert.AreEqual(ids[i], q.Get(i).Value);
            CollectionAssert.AreEqual(bytes, q.Serialize());
            Assert.AreEqual(0, SaveIdPalette.Parse(SaveIdPalette.CreateEmpty().Serialize()).Count);
            CollectionAssert.AreEqual(new byte[] { 0 }, SaveIdPalette.CreateEmpty().Serialize(), "empty IDPL = count 0");
        }

        [Test]
        public void Seeded_KeepsPrefix()
        {
            var loaded = SaveIdPalette.Parse(Palette("a", "b", "c").Serialize());
            var p = SaveIdPalette.SeededFrom(loaded);
            Assert.AreEqual(1, p.GetOrAdd(Id("b")), "loaded entries keep their indices");
            Assert.AreEqual(3, p.GetOrAdd(Id("d")), "new ids append");
            Assert.AreEqual(0, p.GetOrAdd(Id("a")));
            Assert.AreEqual(4, p.Count);
            Assert.AreEqual(3, loaded.Count, "the loaded palette is not modified");
        }

        [Test]
        public void InvalidIndex_Throws()
        {
            var p = Palette("a", "b");
            Assert.Throws<SaveFormatException>(() => p.Get(2));
            Assert.Throws<SaveFormatException>(() => p.Get(0xFFFE));
            Assert.Throws<SaveFormatException>(() => SaveIdPalette.Parse(new byte[] { 2, 1, (byte)'a' }), "truncated");
            Assert.Throws<SaveFormatException>(() => SaveIdPalette.Parse(new byte[0]), "empty payload");
            Assert.Throws<SaveFormatException>(() => SaveIdPalette.Parse(new byte[] { 1, 1, (byte)'a', 9 }), "trailing byte");
        }

        [Test]
        public void Overflow_Throws()
        {
            var entry = new List<byte> { 1, 0x81, 0x02 };           // count 1, length 257
            entry.AddRange(new byte[257]);
            Assert.Throws<SaveFormatException>(() => SaveIdPalette.Parse(entry.ToArray()), "a 257-byte entry");
            Assert.Throws<SaveFormatException>(() => SaveIdPalette.CreateEmpty().GetOrAdd(Id(new string('y', 257))), "writer refuses what it could not read");

            var p = SaveIdPalette.CreateEmpty();
            for (int i = 0; i < SaveIdPalette.MaxEntries; i++) p.GetOrAdd(Id("i" + i));
            Assert.AreEqual(65534, p.GetOrAdd(Id("i65534")));
            Assert.Throws<SaveFormatException>(() => p.GetOrAdd(Id("one_too_many")), "65,536th distinct id");
            Assert.AreEqual(5, p.GetOrAdd(Id("i5")), "existing ids still resolve");
        }

        [Test]
        public void ThroughSaveWriterAndReader()
        {
            var palette = SaveIdPalette.CreateEmpty();
            byte[] payload;
            using (var w = new SaveWriter(palette))
            {
                w.WriteContentId(Id("block_hull")); w.WriteContentId(ContentId.None); w.WriteContentId(Id("item_iron")); w.WriteContentId(Id("block_hull"));
                payload = w.ToArray();
            }
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0xFF, 0xFF, 1, 0, 0, 0 }, payload, "u16 local indices, little-endian");
            var loaded = SaveIdPalette.Parse(palette.Serialize());
            var r = new SaveReader(payload, loaded);
            Assert.AreEqual("block_hull", r.ReadContentId().Value);
            Assert.IsTrue(r.ReadContentId().IsNone);
            Assert.AreEqual("item_iron", r.ReadContentId().Value);
            Assert.AreEqual("block_hull", r.ReadContentId().Value);
            Assert.IsTrue(r.IsAtEnd);
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[] { 7, 0 }, loaded).ReadContentId(), "index past the palette");
        }

        static SaveIdPalette Palette(params string[] ids)
        {
            var p = SaveIdPalette.CreateEmpty();
            foreach (var s in ids) p.GetOrAdd(Id(s));
            return p;
        }
    }
}
