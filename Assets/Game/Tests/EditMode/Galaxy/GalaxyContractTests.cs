using System;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>GAL-001: unmanaged contracts and structural identities (SPEC-30 §4).</summary>
    public sealed class GalaxyContractTests
    {
        [Test]
        public void IdTypes_AreUnmanaged()
        {
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<StarSystemId>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<BodyId>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<BodySpec>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<StarSystemDef>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<LaneDef>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<UniversePosition>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<FrameOrigin>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<OriginRebaseRequest>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<OriginShifted>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<StarSystemMember>());
            Assert.IsTrue(typeof(ISharedComponentData).IsAssignableFrom(typeof(StarSystemMember)));
            Assert.IsFalse(typeof(IComponentData).IsAssignableFrom(typeof(StarSystemMember)));
        }

        [Test]
        public void BodyId_LocalKey_RoundTrips()
        {
            var system = new StarSystemId(17);
            var otherSystem = new StarSystemId(18);
            ushort[] slots = { 0, 1, 32768, ushort.MaxValue };
            foreach (BodyRole role in Enum.GetValues(typeof(BodyRole)))
                foreach (ushort slot in slots)
                    foreach (ushort subSlot in slots)
                    {
                        var original = new BodyId(system, role, slot, subSlot);
                        ulong expected = ((ulong)role << 32) | ((ulong)slot << 16) | subSlot;
                        Assert.AreEqual(expected, original.LocalKey);
                        var restored = BodyId.FromLocalKey(system, expected);
                        Assert.AreEqual(system, restored.System);
                        Assert.AreEqual(role, restored.Role);
                        Assert.AreEqual(slot, restored.Slot);
                        Assert.AreEqual(subSlot, restored.SubSlot);
                        Assert.IsTrue(original.Equals(restored));
                        Assert.AreEqual(StableHash.Combine(system.Value, expected), restored.StableKey);
                        Assert.IsFalse(original.Equals(BodyId.FromLocalKey(otherSystem, expected)), "equal local keys in different star systems are distinct bodies");
                        Assert.AreEqual(role != BodyRole.None, restored.IsValid);
                    }
            Assert.IsFalse(default(BodyId).IsValid);
            Assert.IsFalse(new BodyId(StarSystemId.None, BodyRole.Star, 0, 0).IsValid);
        }

        [Test]
        public void StarSystemId_Authored_SetsHighBit()
        {
            ulong[] hashes = { 0UL, 1UL, 0x7FFFFFFFUL, 0x80000000UL, 0x12345678FEDCBA98UL, ulong.MaxValue };
            foreach (ulong value in hashes)
            {
                var id = StarSystemId.FromAuthored(new ContentHash(value));
                uint low = (uint)(value & 0x7FFFFFFFUL);
                Assert.AreEqual(0x80000000u | (low == 0 ? 1u : low), id.Value);
                Assert.IsTrue(id.IsValid);
                Assert.IsTrue(id.IsAuthored);
                Assert.AreNotEqual(0u, id.Value & 0x7FFFFFFFu);
            }
            Assert.AreEqual(1u, StarSystemId.FromProceduralIndex(0).Value);
            Assert.AreEqual(0x7FFFFFFEu, StarSystemId.FromProceduralIndex(0x7FFFFFFD).Value);
            Assert.IsFalse(StarSystemId.FromProceduralIndex(0x7FFFFFFD).IsAuthored);
            Assert.IsFalse(StarSystemId.None.IsValid);
            Assert.IsFalse(StarSystemId.None.IsAuthored);
        }
    }
}
