using Game.Data.Components;
using Game.Simulation;
using Game.Simulation.Weapons;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>WPN-001: unmanaged combat contracts and default faction relationships (SPEC-21 §9).</summary>
    public sealed class WeaponContractTests
    {
        [Test]
        public void ProjectileRow_Is64Bytes()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<ProjectileRow>());
        }

        [Test]
        public void FactionDefaults()
        {
            var relations = FactionRelations.CreateDefault(Allocator.Temp);
            try
            {
                Assert.AreEqual(3, relations.Count);
                Assert.AreEqual(9, relations.Stance.Length);
                for (byte a = 0; a < 3; a++)
                for (byte b = 0; b < 3; b++)
                {
                    var expected = a == b ? FactionStance.Friendly :
                        a < 2 && b < 2 ? FactionStance.Hostile : FactionStance.Neutral;
                    Assert.AreEqual(expected, relations.Get(a, b), $"Faction {a} -> {b}");
                    Assert.AreEqual(expected == FactionStance.Hostile,
                        relations.IsHostile(new Allegiance { Faction = a }, new Allegiance { Faction = b }));
                }
                Assert.AreEqual(FactionStance.Neutral, relations.Get(255, 0));
                Assert.AreEqual(FactionStance.Neutral, relations.Get(0, 3));
                Assert.IsFalse(relations.IsHostile(Allegiance.None, Allegiance.Player));
                Assert.IsFalse(relations.IsHostile(Allegiance.Player, Allegiance.None));
                Assert.IsTrue(Allegiance.Player.Equals(new Allegiance { Faction = 0, Team = 0 }));
                Assert.IsFalse(Allegiance.Player.Equals(new Allegiance { Faction = 0, Team = 1 }));
                Assert.AreEqual(255, Allegiance.None.Faction);
            }
            finally { relations.Dispose(); }
        }

        [Test]
        public void Components_AreUnmanaged()
        {
            Check<OwnedBy>(); Check<Allegiance>(); Check<FactionRelations>();
            Check<WeaponDefsRef>(); Check<WeaponTuning>(); Check<WeaponMount>();
            Check<WeaponAim>(); Check<WeaponState>(); Check<BeamState>();
            Check<WeaponControl>(); Check<ShipTarget>(); Check<WeaponStats>();
            Check<ProjectileStore>(); Check<WeaponShots>();
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<WeaponDef>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<WeaponDefsBlob>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<ProjectileRow>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<ProjectileStoreStats>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<WeaponShot>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<CombatHit>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<CombatHitTest>());
            Assert.AreEqual("Game.Data", typeof(OwnedBy).Assembly.GetName().Name);
            TestContext.WriteLine("WeaponShot layout: " + UnsafeUtility.SizeOf<WeaponShot>() + " bytes");
        }

        private static void Check<T>() where T : unmanaged, IComponentData
            => Assert.IsTrue(UnsafeUtility.IsUnmanaged<T>(), typeof(T).Name);
    }
}
