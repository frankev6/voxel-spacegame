using Game.App.DevRuntime;
using Game.App.DevRuntime.Commands;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tests.PlayMode.DevTools
{
    public sealed class AimCommandTests : InputTestFixture
    {
        [Test] public void NearestHostile_MouseOnTarget()
        {
            using var f = new CombatCommandFixture();
            var friendly = f.Grid(2, new float3(0, 0, 1), 0); f.Grid(30, new float3(0, 0, 6), 1);
            var result = f.Command("aim target=nearest_hostile"); Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text);
            InputSystem.Update(); f.AssertMouse(f.Hostile);
            result = f.Command("aim target=nearest"); Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text);
            InputSystem.Update(); f.AssertMouse(friendly);
            result = f.Command("aim target=id:20"); Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text);
            InputSystem.Update(); f.AssertMouse(f.Hostile);
            Vector2 before = f.Host.Input.MouseDevice.position.ReadValue();
            f.Rig.Camera.transform.rotation = Quaternion.LookRotation(Vector3.back);
            result = f.Command("aim target=id:20"); Assert.AreEqual(DevResultKind.Failed, result.Kind); StringAssert.Contains("behind camera", result.Text);
            InputSystem.Update(); Assert.AreEqual(before, f.Host.Input.MouseDevice.position.ReadValue());
            f.Rig.Camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, -20, 20));
            Assert.AreEqual(DevResultKind.Failed, f.Command("aim target=point x=1").Kind);
            result = f.Command("aim target=point x=1 z=2"); Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text);
            InputSystem.Update(); Vector3 screen = f.Rig.Camera.WorldToScreenPoint(new Vector3(1, 0, 2));
            Assert.Less(Vector2.Distance(new Vector2(screen.x, screen.y), f.Host.Input.MouseDevice.position.ReadValue()), 2);
            var registry = new DevCommandRegistry(new AimDevCommand(() => f.Now));
            result = registry.Execute("aim target=id:20 track=1", f.Host.Context); Assert.AreEqual(DevResultKind.Pending, result.Kind, result.Text);
            Assert.IsTrue(f.Host.TryGetOperation(result.OperationId, out var operation));
            f.Em.SetComponentData(f.Hostile, LocalTransform.FromPosition(new float3(2, 0, 3)));
            f.Host.Update(); InputSystem.Update(); f.AssertMouse(f.Hostile);
            f.Now += 1; f.Host.Update(); Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status, operation.Message);
        }
    }
}
