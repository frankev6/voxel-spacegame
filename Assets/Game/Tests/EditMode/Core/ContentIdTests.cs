using Game.Core;
using Game.Core.Components;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-002: ContentId and ContentHash (SPEC-02 §4.1).</summary>
    public sealed class ContentIdTests
    {
        [Test]
        public void Empty_IsNone()
        {
            Assert.IsTrue(new ContentId("").IsNone);
            Assert.IsTrue(new ContentId(null).IsNone);
            Assert.IsTrue(ContentId.None.IsNone);
            Assert.AreEqual(0UL, new ContentId("").Hash.Value);
            Assert.AreEqual(ContentId.None, new ContentId(""));
            Assert.AreEqual("<none>", ContentId.None.ToString());
        }

        [Test]
        public void Hash_EqualsStableHash()
        {
            var id = new ContentId("block_hull");
            Assert.AreEqual(0x208BCD2F5794080CUL, id.Hash.Value);
            Assert.AreEqual(StableHash.Hash64("ship_frigate"), new ContentId("ship_frigate").Hash.Value);
            Assert.IsFalse(id.IsNone); Assert.IsFalse(id.Hash.IsNone);
        }

        [Test]
        public void Equality_ComparesHashAndString()
        {
            Assert.AreEqual(new ContentId("block_hull"), new ContentId("block_hull"));
            Assert.IsTrue(new ContentId("block_hull") == new ContentId("block_hull"));
            Assert.AreNotEqual(new ContentId("block_hull"), new ContentId("block_hull_angled"));
            Assert.IsTrue(new ContentId("a_b") != new ContentId("A_b"), "ordinal");
            Assert.AreEqual(new ContentId("block_hull").GetHashCode(), new ContentId("block_hull").GetHashCode());
        }

        [Test]
        public void ContentHash_IsUnmanaged()
        {
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<ContentHash>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<PersistentId>());
            Assert.AreEqual(8, UnsafeUtility.SizeOf<ContentHash>());
        }
    }
}
