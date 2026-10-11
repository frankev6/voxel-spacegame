using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-003: SimTime (SPEC-02 §5.2).</summary>
    public sealed class SimTimeTests
    {
        [Test]
        public void SecondsToTicks_Rounds()
        {
            Assert.AreEqual(30u, SimTime.SecondsToTicks(0.5f));
            Assert.AreEqual(1u, SimTime.SecondsToTicks(1 / 120f), "half a tick rounds up");
            Assert.AreEqual(0u, SimTime.SecondsToTicks(-1f));
            Assert.AreEqual(0u, SimTime.SecondsToTicks(0f));
            Assert.AreEqual(0u, SimTime.SecondsToTicks(float.NaN));
            Assert.AreEqual(60u, SimTime.SecondsToTicks(1f));
            Assert.AreEqual(uint.MaxValue, SimTime.SecondsToTicks(1e12f), "clamped");
            Assert.AreEqual(1.0, SimTime.TicksToSeconds(60), 1e-12);
        }
    }
}
