using System;
using System.Linq;
using System.Reflection;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Hazards;
using Game.Simulation.Mechanisms;
using Game.Simulation.Power;
using NUnit.Framework;

namespace Game.Tests.EditMode.Simulation.Hazards
{
    /// <summary>HAZ-012: the SPEC-91 §4.6 mechanism contract (ADR-0119) exists, and every enum change is an append.</summary>
    public sealed class MechanismContractTests
    {
        static byte[] Values<T>() where T : Enum => Enum.GetValues(typeof(T)).Cast<T>().Select(v => Convert.ToByte(v)).ToArray();

        [Test]
        public void EnumValues_Appended()
        {
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, Values<ZoneEffectKind>());
            Assert.AreEqual(5, (byte)ZoneEffectKind.Attract); Assert.AreEqual(6, (byte)ZoneEffectKind.Pulse);
            Assert.AreEqual(10, (byte)BlockModuleId.ProximityFuse);
            Assert.AreEqual(11, (byte)BlockModuleId.Switch); Assert.AreEqual(12, (byte)BlockModuleId.Pickup);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, Values<MinePayload>());
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, Values<MoverPath>());
            CollectionAssert.AreEqual(Enumerable.Range(0, 12).Select(i => (byte)i).ToArray(), Values<MechanismActionKind>());
            // The authoring enum mirrors the runtime one value for value, so baking is a cast.
            foreach (MechanismActionKind kind in Enum.GetValues(typeof(MechanismActionKind)))
                Assert.AreEqual(kind.ToString(), ((MechanismDefinition.ActionKind)(byte)kind).ToString());
            Assert.AreEqual(Values<MechanismActionKind>().Length, Values<MechanismDefinition.ActionKind>().Length);
        }

        [Test]
        public void Modules_Registered()
        {
            Assert.AreEqual(BlockModuleId.Switch, new SwitchModule().Id);
            Assert.AreEqual(BlockModuleId.Pickup, new PickupModule().Id);
            // The fuse keeps its HAZ-001/HAZ-008 fields first; the ADR-0119 payload fields are appended after them.
            var fields = typeof(ProximityFuseParams).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            CollectionAssert.AreEqual(new[] { "ArmSeconds", "TriggerRadius", "FuseSeconds", "BlastRadius", "BlastDamage", "BlastType",
                "MinTriggerMass", "TriggerFilter", "CasingHp", "BlastImpulse", "Payload", "PayloadStrength", "PayloadSeconds", "SeekSpeed" }, fields);
            Assert.AreEqual(MinePayload.Blast, default(ProximityFuseParams).Payload);
            Assert.AreEqual("mechanism_", typeof(MechanismDefinition).GetCustomAttribute<ContentIdPrefixAttribute>().Prefixes.Single());
            Assert.IsNotNull(typeof(StatusChannels).GetField(nameof(StatusChannels.EnergyGrants)));
            Assert.IsTrue(typeof(Unity.Entities.IComponentData).IsAssignableFrom(typeof(Mover)));
            Assert.IsTrue(typeof(Unity.Entities.IBufferElementData).IsAssignableFrom(typeof(MoverPoint)));
            Assert.IsTrue(typeof(Unity.Entities.IComponentData).IsAssignableFrom(typeof(Game.Simulation.Hazards.ZoneSchedule)));
        }
    }
}
