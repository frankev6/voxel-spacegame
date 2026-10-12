using Game.Simulation.Ai;
using Game.Simulation.Spatial;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Tests.EditMode.Simulation.Ai
{
    /// <summary>AI-001: SPEC-23 §9 proxy layout and unmanaged simulation contracts.</summary>
    public sealed class AiContractTests
    {
        [Test]
        public void SpatialProxy_Is64Bytes()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<SpatialProxy>());
        }

        [Test]
        public void Components_AreUnmanaged()
        {
            Check<SpatialBody>(); Check<SpatialHash>(); Check<AiConfig>();
            Check<AiBrain>(); Check<PlayerCombatStatus>(); Check<FleetMember>();
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<SpatialProxy>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<SpatialHashState>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<AiProfile>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<AiAbilityRuleDef>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<AiProfilesBlob>());
        }

        private static void Check<T>() where T : unmanaged, IComponentData
            => Assert.IsTrue(UnsafeUtility.IsUnmanaged<T>(), typeof(T).Name);
    }
}
