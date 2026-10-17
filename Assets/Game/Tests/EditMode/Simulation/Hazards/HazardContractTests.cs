using System;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Data.Hazards;
using Game.Simulation.Hazards;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;

namespace Game.Tests.EditMode.Simulation.Hazards
{
    /// <summary>HAZ-001: the SPEC-91 contract types exist, and every enum change is an append (ADR-0118).</summary>
    public sealed class HazardContractTests
    {
        [Test]
        public void EnumValues_Appended()
        {
            Assert.AreEqual(10, (byte)ProducerKind.MainThread);
            Assert.AreEqual(11, (byte)ProducerKind.Recoil);
            Assert.AreEqual(12, (byte)ProducerKind.Hazard);
            Assert.AreEqual(255, (byte)ProducerKind.Test);
            Assert.AreEqual(3, (byte)GridKind.Hulk);
            Assert.AreEqual(4, (byte)GridKind.Device);
            Assert.AreEqual(9, (byte)BlockModuleId.SalvageMagnet);
            Assert.AreEqual(10, (byte)BlockModuleId.ProximityFuse);
            Assert.AreEqual(25, (ushort)RngPurpose.Salvage);
            Assert.AreEqual(26, (ushort)RngPurpose.HazardScatter);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, Enum.GetValues(typeof(ZoneEffectKind)).Cast<ZoneEffectKind>().Select(k => (byte)k).ToArray());
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2 }, Enum.GetValues(typeof(ZoneShape)).Cast<ZoneShape>().Select(k => (byte)k).ToArray());
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, Enum.GetValues(typeof(MinePhase)).Cast<MinePhase>().Select(k => (byte)k).ToArray());
        }

        [Test]
        public void GridRoot_HasExternalPush()
        {
            CollectionAssert.Contains(DamageSetup.GridRootComponents(), ComponentType.ReadWrite<ExternalPush>());
        }

        [Test]
        public void Definitions_HavePrefixes()
        {
            Assert.AreEqual("zone_", typeof(ZoneDefinition).GetCustomAttribute<ContentIdPrefixAttribute>().Prefixes.Single());
            Assert.AreEqual("mine_", typeof(MineDefinition).GetCustomAttribute<ContentIdPrefixAttribute>().Prefixes.Single());
            Assert.AreEqual("minefield_", typeof(MineFieldDefinition).GetCustomAttribute<ContentIdPrefixAttribute>().Prefixes.Single());
        }

        [Test]
        public void HazardSpawn_IsAContractOnly()
        {
            Assert.Throws<NotImplementedException>(() => HazardSpawn.DespawnGroup(default, 1));
        }

        [Test]
        public void FlightConfigDefault_HasNoCarry()
        {
            var c = Game.Simulation.Flight.FlightConfig.Default;   // isolated flight fixtures behave as before carry (HAZ-002)
            Assert.GreaterOrEqual(c.CarryDecel, 1e5f);
            Assert.GreaterOrEqual(c.CarryYawDecel, 1e5f);
        }
    }
}
