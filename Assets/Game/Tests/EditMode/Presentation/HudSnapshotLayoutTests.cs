using Game.Presentation.Components;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>HUD-001: canonical snapshot sizes and unmanaged ECS storage (SPEC-61 §4.5).</summary>
    [Category("UI")]
    public sealed class HudSnapshotLayoutTests
    {
        [Test]
        public void Sizes_AsSpecified()
        {
            Assert.AreEqual(8, UnsafeUtility.SizeOf<HudWeaponGroupState>());
            Assert.AreEqual(16, UnsafeUtility.SizeOf<HudAbilityState>());
            Assert.AreEqual(12, UnsafeUtility.SizeOf<HudRadarBlip>());
        }

        [Test]
        public void Unmanaged()
        {
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<HudSnapshot>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<HudRadarBlip>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<HudWeaponGroupState>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<HudAbilityState>());
            Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(typeof(HudSnapshot)));
            Assert.IsTrue(typeof(IBufferElementData).IsAssignableFrom(typeof(HudRadarBlip)));
            Assert.AreEqual(0, ((InternalBufferCapacityAttribute)System.Attribute.GetCustomAttribute(
                typeof(HudRadarBlip), typeof(InternalBufferCapacityAttribute))).Capacity);
        }
    }
}
