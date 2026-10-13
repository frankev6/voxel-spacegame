using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Simulation.Abilities;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Game.Tests.EditMode.Simulation.Abilities
{
    /// <summary>ABL-001: canonical ability slot layout and shared stat identity (SPEC-24 §4).</summary>
    public sealed class AbilityContractTests
    {
        [Test]
        public void Slot_Is48Bytes()
        {
            Assert.AreEqual(48, UnsafeUtility.SizeOf<AbilitySlot>());
            Assert.AreEqual(typeof(StatId), typeof(AbilityStatMod).GetField(nameof(AbilityStatMod.Stat)).FieldType);
        }
    }
}
