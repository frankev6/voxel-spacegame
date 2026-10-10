using System;
using System.Linq;
using Game.Data.Blocks;
using NUnit.Framework;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-001 contract checks (SPEC-04 §9).</summary>
    public class ContractShapeTests
    {
        [Test]
        public void BlockKind_HasBindingValues()
        {
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(BlockKind)));
            var values = Enum.GetValues(typeof(BlockKind)).Cast<BlockKind>().ToArray();
            CollectionAssert.AreEqual(new[] { BlockKind.Structural, BlockKind.Prop, BlockKind.Functional }, values);
            Assert.AreEqual(0, (byte)BlockKind.Structural);
            Assert.AreEqual(1, (byte)BlockKind.Prop);
            Assert.AreEqual(2, (byte)BlockKind.Functional);
        }

        [Test]
        public void OrientationMask_Allows_RespectsBits()
        {
            var yaw = new OrientationMask { Bits = OrientationMask.YawOnlyBits };
            for (byte o = 0; o < 24; o++)
                Assert.AreEqual(o == 0 || o == 4 || o == 8 || o == 12, yaw.Allows(o), $"yaw-only, orientation {o}");

            var all = OrientationMask.All;
            for (byte o = 0; o < 24; o++) Assert.IsTrue(all.Allows(o), $"all, orientation {o}");
            Assert.IsFalse(all.Allows(24), "24 is out of range");
            Assert.IsFalse(new OrientationMask { Bits = 0xFFFFFFFF }.Allows(Orientation.Invalid), "255 is never allowed");

            var horizontal = new OrientationMask { Bits = OrientationMask.HorizontalForwardBits };
            Assert.IsTrue(horizontal.Allows(15));
            Assert.IsFalse(horizontal.Allows(16));
            Assert.IsFalse(default(OrientationMask).Allows(0));
        }
    }
}
