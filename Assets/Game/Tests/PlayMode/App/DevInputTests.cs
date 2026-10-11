using System.Collections;
using System.Linq;
using Game.App.DevRuntime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    public sealed class DevInputTests
    {
        DevRuntimeHost host;
        Mouse testMouse;
        Keyboard testKeyboard;
        [UnityTearDown] public IEnumerator TearDown()
        {
            host?.Dispose();
            if (testMouse != null && testMouse.added) InputSystem.RemoveDevice(testMouse);
            if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
            yield return null;
        }
        [UnityTest] public IEnumerator KeyHold_ReleasesAfterHold()
        {
            if (InputSystem.GetDevice<Keyboard>() == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            host = DevRuntimeHost.Create(null);
            var keyboard = InputSystem.GetDevice<Keyboard>();
            Assert.AreEqual(DevResultKind.Ok, host.Commands.Execute("key keys=W hold=0.2", host.Context).Kind);
            InputSystem.Update();
            Assert.IsTrue(keyboard.wKey.isPressed);
            double start = Time.unscaledTimeAsDouble, last = start, frame = 0;
            double limit = Time.realtimeSinceStartupAsDouble + 5;
            while (keyboard.wKey.isPressed && Time.realtimeSinceStartupAsDouble < limit)
            {
                yield return null;
                frame = System.Math.Max(frame, Time.unscaledTimeAsDouble - last); last = Time.unscaledTimeAsDouble;
            }
            Assert.IsFalse(keyboard.wKey.isPressed, "host releases without another command");
            Assert.That(Time.unscaledTimeAsDouble - start, Is.InRange(.2 - frame, .2 + 2 * frame + .02));
        }
        [UnityTest] public IEnumerator AgentMouse_RestoresNativeMice()
        {
            testMouse = InputSystem.AddDevice<Mouse>("DevTestNativeMouse");
            var enabled = InputSystem.devices.OfType<Mouse>().Where(d => d.enabled).ToArray();
            host = DevRuntimeHost.Create(null);
            Assert.AreEqual(DevResultKind.Ok, host.Commands.Execute("mouse x=42 y=21 button=left", host.Context).Kind);
            InputSystem.Update();
            Assert.IsTrue(host.Input.MouseDevice.leftButton.isPressed);
            Assert.AreEqual(new Vector2(42, 21), host.Input.MouseDevice.position.ReadValue());
            foreach (var device in enabled) Assert.IsFalse(device.enabled);
            host.Dispose(); yield return null;
            foreach (var device in enabled) Assert.IsTrue(device.enabled);
            Assert.IsFalse(InputSystem.devices.Any(d => d.name == "AgentMouse"));
        }
    }
}
