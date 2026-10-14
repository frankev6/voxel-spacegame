using Game.App.DevRuntime;
using Game.App.DevRuntime.Commands;
using Game.Core;
using Game.Data.Definitions;
using Game.Input;
using Game.Presentation.Input;
using Game.Presentation.Vfx;
using Game.Simulation.Commands;
using Game.Simulation.Weapons;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Game.Tests.PlayMode.DevTools
{
    public sealed class FireCommandTests : InputTestFixture
    {
        [Test] public void Group1_FiresWeapons()
        {
            using var f = new CombatCommandFixture(true);
            Assert.AreEqual(DevResultKind.Ok, f.Command("aim target=nearest_hostile").Kind); InputSystem.Update();
            var registry = new DevCommandRegistry(new FireDevCommand(() => f.Now));
            var result = registry.Execute("fire group=1 seconds=1", f.Host.Context);
            Assert.AreEqual(DevResultKind.Pending, result.Kind, result.Text);
            Assert.IsTrue(f.Host.TryGetOperation(result.OperationId, out var operation));
            InputSystem.Update(); f.Test.UpdateManaged<PlayerInputSampleSystem>();
            f.Test.UpdateSystem<PlayerCommandApplySystem>(); f.Test.UpdateSystem<WeaponFireSystem>(); f.Em.CompleteAllTrackedJobs();
            Assert.AreEqual(1, f.Em.GetComponentData<ShipCommand>(f.Player).FireGroups & 1);
            Assert.Greater(f.Em.GetComponentData<WeaponState>(f.Mount).ShotCounter, 0);
            var fx = f.Test.GetSingleton<FxEventQueue>(); fx.Access.CompleteAccess();
            Assert.Greater(fx.Queue.Count, 0); Assert.AreEqual(FxKind.Muzzle, fx.Queue.Peek().Kind);
            var upload = f.Test.World.GetExistingSystemManaged<FxUploadSystem>(); upload.Update();
            Assert.Greater(upload.Stats.EventsDrained, 0, "The production FX drain counted the player's Muzzle event.");
            f.Now += 1; f.Host.Input.Pump(f.Now); f.Host.Update(); InputSystem.Update(); f.Test.UpdateManaged<PlayerInputSampleSystem>();
            Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status, operation.Message);
            Assert.IsFalse(f.Input.Controls.Flight.FirePrimary.IsPressed());
            Assert.AreEqual(DevResultKind.Failed, f.Command("fire group=0").Kind);
            Assert.AreEqual(DevResultKind.Failed, f.Command("fire seconds=0").Kind);
            var action = f.Input.Controls.Flight.FirePrimary;
            for (int i = 0; i < action.bindings.Count; i++) action.ApplyBindingOverride(i, "");
            result = f.Command("fire group=1"); Assert.AreEqual(DevResultKind.Failed, result.Kind); StringAssert.Contains("Flight/FirePrimary has no binding", result.Text);
            action.RemoveAllBindingOverrides(); f.Input.SetContext(InputContext.TextEntry);
            Assert.AreEqual(DevResultKind.Failed, f.Command("fire").Kind);
        }
    }
}
