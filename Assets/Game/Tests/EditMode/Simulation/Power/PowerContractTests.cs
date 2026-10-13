using System;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Simulation.Power;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>Implements SPEC-22 §9 PWR-001: layout, tuning defaults and stable stat identifiers.</summary>
    public sealed class PowerContractTests
    {
        [Test]
        public void Sizes_MatchSpec()
        {
            Assert.AreEqual(24, UnsafeUtility.SizeOf<PowerConsumer>());
            Assert.AreEqual(4, UnsafeUtility.SizeOf<PowerLink>());
            Assert.AreEqual(12, UnsafeUtility.SizeOf<PowerRequest>());
            Assert.AreEqual(20, UnsafeUtility.SizeOf<ShieldRipple>());
        }

        [Test]
        public void TuningDefaults_MatchDefinitionDefaults()
        {
            var definition = ScriptableObject.CreateInstance<PowerTuningDefinition>();
            try
            {
                var tuning = PowerTuning.Defaults;
                Assert.AreEqual(definition.PriorityShieldEmitter, tuning.PriorityShieldEmitter);
                Assert.AreEqual(definition.PriorityThruster, tuning.PriorityThruster);
                Assert.AreEqual(definition.PriorityAbility, tuning.PriorityAbility);
                Assert.AreEqual(definition.PriorityWeapon, tuning.PriorityWeapon);
                Assert.AreEqual(definition.BrownoutHoldTicks, tuning.BrownoutHoldTicks);
                Assert.AreEqual(definition.ShieldPaddingScale, tuning.ShieldPaddingScale);
                Assert.AreEqual(definition.ShieldPaddingMetres, tuning.ShieldPaddingMetres);
                Assert.AreEqual(definition.ShieldMinRadiusMetres, tuning.ShieldMinRadiusMetres);
                Assert.AreEqual(definition.ShieldRestartFraction, tuning.ShieldRestartFraction);
                Assert.AreEqual(definition.SustainWarningSeconds, tuning.SustainWarningSeconds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void StatId_ValuesAreStable()
        {
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(StatId)));
            Assert.AreEqual(0, (byte)StatId.Thrust);
            Assert.AreEqual(1, (byte)StatId.MaxSpeed);
            Assert.AreEqual(2, (byte)StatId.FireRate);
            Assert.AreEqual(3, (byte)StatId.WeaponDamage);
            Assert.AreEqual(4, (byte)StatId.ShieldRegen);
            Assert.AreEqual(5, (byte)StatId.TurnRate);
        }
    }
}
