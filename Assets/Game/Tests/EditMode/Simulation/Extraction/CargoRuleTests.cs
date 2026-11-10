using Game.Simulation.Extraction;
using Game.Tests.EditMode.Simulation.Hazards;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Extraction
{
    /// <summary>EXT-003 (SPEC-93 decision 4): the cargo rule and stowing.</summary>
    public sealed class CargoRuleTests : ZoneFixtureBase
    {
        protected override System.Type[] Systems => new[] { typeof(DamageStreamsSystem), typeof(TowSystem), typeof(ImpulseApplySystem) };

        [Test] public void NoHold_CannotJumpWithALoad()
        {
            var ship = Body(new float2(0, 0), float2.zero); var load = Body(new float2(0, -20), float2.zero, 600000);
            Assert.That(StowApi.CanJump(EM, ship, out _, out _), Is.True, "no load: nothing to check");
            TowApi.Attach(EM, ship, load, 25, Tick);
            Assert.That(StowApi.CanJump(EM, ship, out float loadT, out float freeT), Is.False);
            Assert.That(loadT, Is.EqualTo(600).Within(.01f)); Assert.That(freeT, Is.EqualTo(0));
            EM.AddComponentData(ship, new CargoHold { CapacityT = 650 });
            Assert.That(StowApi.CanJump(EM, ship, out _, out _), Is.True);
        }

        [Test] public void Stow_NeedsRoom_AndUnstowPutsItBehind()
        {
            var ship = Body(new float2(0, 0), new float2(0, 3)); var load = Body(new float2(0, -20), float2.zero, 40000);
            TowApi.Attach(EM, ship, load, 25, Tick);
            EM.AddComponentData(ship, new CargoHold { CapacityT = 30 });
            Assert.That(StowApi.Stow(EM, ship, out var reason), Is.False); Assert.That(reason, Does.Contain("40"));
            EM.SetComponentData(ship, new CargoHold { CapacityT = 50 });
            Assert.That(StowApi.Stow(EM, ship, out _), Is.True);
            Assert.That(EM.HasComponent<Disabled>(load) && EM.HasComponent<Stowed>(load), Is.True);
            Assert.That(TowApi.IsTowing(EM, ship), Is.False);
            Assert.That(EM.GetComponentData<CargoHold>(ship).FreeT, Is.EqualTo(10).Within(.01f));
            Assert.That(StowApi.Unstow(EM, ship, load, 30), Is.True);
            Assert.That(EM.HasComponent<Disabled>(load), Is.False);
            Assert.That(math.distance(Position(load), new float2(0, -30)), Is.LessThan(.01f));
            Assert.That(EM.GetComponentData<CargoHold>(ship).StowedT, Is.EqualTo(0).Within(.01f));
        }
    }
}
