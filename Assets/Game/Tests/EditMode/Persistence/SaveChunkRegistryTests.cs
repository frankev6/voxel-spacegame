using System;
using System.Linq;
using Game.Persistence.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-001: the chunk registry (SPEC-05 §4.8).</summary>
    public sealed class SaveChunkRegistryTests
    {
        [Test]
        public void Default_MatchesSpecTable()
        {
            var expected = new (string key, int order, string owner)[]
            {
                ("SESS", 10, "SPEC-01"), ("FACT", 20, "SPEC-40"), ("FACN", 30, "SPEC-33"), ("QUST", 40, "SPEC-42"), ("DLGS", 50, "SPEC-41"),
                ("INVT", 60, "SPEC-50"), ("FLET", 70, "SPEC-01"), ("GLXY", 80, "SPEC-30"), ("PLOC", 90, "SPEC-31"), ("ENCT", 100, "SPEC-33"),
            };
            var actual = SaveChunkRegistry.Default.Entries.Select(e => (e.Key, e.Order, e.OwnerSpec)).ToArray();
            CollectionAssert.AreEqual(expected, actual);
            Assert.IsTrue(SaveChunkRegistry.Default.Entries.All(e => !string.IsNullOrEmpty(e.Description)));
            Assert.IsTrue(SaveChunkRegistry.Default.TryGetOrder("FLET", out int order)); Assert.AreEqual(70, order);
            Assert.IsFalse(SaveChunkRegistry.Default.TryGetOrder("META", out _), "META belongs to the container");
            Assert.IsFalse(SaveChunkRegistry.Default.TryGetOrder("ZZZZ", out _));
        }

        static ChunkRegistration R(string key, int order) => new ChunkRegistration(key, order, "TEST", "test chunk");

        [Test]
        public void Constructor_RejectsBadKeysAndDuplicateOrders()
        {
            var ok = new SaveChunkRegistry(new[] { R("TSTC", 1020), R("TSTA", 1000), R("TSTB", 1010) });
            CollectionAssert.AreEqual(new[] { "TSTA", "TSTB", "TSTC" }, ok.Entries.Select(e => e.Key).ToArray(), "sorted by order");

            foreach (var bad in new[] { "tsta", "TST", "TSTAA", "TS-A", "", null })
                Assert.Throws<ArgumentException>(() => new SaveChunkRegistry(new[] { R(bad, 1000) }), $"key '{bad}'");
            Assert.Throws<ArgumentException>(() => new SaveChunkRegistry(new[] { R("META", 1000) }), "reserved META");
            Assert.Throws<ArgumentException>(() => new SaveChunkRegistry(new[] { R("IDPL", 1000) }), "reserved IDPL");
            Assert.Throws<ArgumentException>(() => new SaveChunkRegistry(new[] { R("TSTA", 1000), R("TSTA", 1010) }), "duplicate key");
            Assert.Throws<ArgumentException>(() => new SaveChunkRegistry(new[] { R("TSTA", 1000), R("TSTB", 1000) }), "duplicate order");
            Assert.Throws<ArgumentException>(() => new SaveChunkRegistry(new[] { R("TSTA", 1005) }), "order not a multiple of 10");
            Assert.Throws<ArgumentException>(() => new SaveChunkRegistry(new[] { R("TSTA", 0) }), "order 0");
            Assert.Throws<ArgumentNullException>(() => new SaveChunkRegistry(null));
        }
    }
}
