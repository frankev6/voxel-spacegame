#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Console;
using Game.Core;
using Game.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.App
{
    public sealed class DevConsoleTests : ProductionSessionFixture
    {
        Keyboard keyboard;
        [UnitySetUp] public IEnumerator AddKeyboard()
        {
            keyboard = InputSystem.AddDevice<Keyboard>("ConsoleTestKeyboard");
            Game.Input.Controls.asset.devices = new InputDevice[] { keyboard }; yield return null;
        }
        [UnityTearDown] public IEnumerator RemoveKeyboard()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null; yield return null;
        }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static void Send(TextField input, KeyCode code)
        {
            using var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = code });
            evt.target = input; input.SendEvent(evt);
        }
        [UnityTest] public IEnumerator Toggle_ChangesContext()
        {
            yield return Run(Game.Session.NewGameAsync(new NewGameArgs("Harness", 42, false, ContentId.None)));
            Keys(Key.W); yield return Until(() => Command.Thrust.y == 1, "Flight input starts");
            Keys(Key.W, Key.F1); yield return Until(() => Host.Console.Screen.IsOpen, "F1 opens console");
            Assert.AreEqual(InputContext.TextEntry, Game.Input.CurrentContext);
            Assert.IsFalse(Game.Pause.IsPaused); Assert.IsTrue(Game.UI.Navigator.WorldPointerBlocked);
            yield return Until(() => Command.Thrust.y == 0, "Typing disables held flight input");
            var firstView = Host.Console.Screen.Root;
            Keys(Key.W); yield return null;
            Keys(Key.W, Key.F1); yield return Until(() => !Host.Console.Screen.IsOpen, "F1 closes console");
            yield return Until(() => Game.Input.CurrentContext == InputContext.Flight && Command.Thrust.y == 1, "Flight resumes after console");
            Keys(); yield return null; Keys(Key.F1); yield return Until(() => Host.Console.Screen.IsOpen, "Reopen console");
            Assert.AreSame(firstView, Host.Console.Screen.Root, "View is built once and cached"); Keys();
        }
        [UnityTest] public IEnumerator History_And_Completion()
        {
            Keys(Key.F1); yield return Until(() => Host.Console.Screen.IsOpen, "Console opens"); Keys(); yield return null;
            var console = Host.Console; var input = console.Screen.Input;
            input.value = "help"; Send(input, KeyCode.Return);
            Assert.IsTrue(console.Lines.Any(line => line.Contains("newgame")), "Help from production registry is displayed");
            Send(input, KeyCode.UpArrow); Assert.AreEqual("help", input.value);
            Send(input, KeyCode.DownArrow); Assert.AreEqual(string.Empty, input.value);
            input.value = "newg"; Send(input, KeyCode.Tab); Assert.AreEqual("newgame", input.value);
            Assert.AreEqual("teleport", DevConsole.Complete("tel", new DevCommandRegistry(new TeleportNameOnly()).Commands));
            input.value = "newgame seed=415"; Send(input, KeyCode.Return);
            yield return Until(() => console.Lines.Any(line => line.Contains("Succeeded")), "Pending result is printed after the real new-game operation", 30);
            Assert.IsTrue(Game.Session.HasActiveSession); Assert.LessOrEqual(console.Lines.Count, 200);
        }
        [UnityTest, Category("Performance")] public IEnumerator Closed_NoGcAlloc()
        {
            var console = Host.Console; Assert.IsFalse(console.Screen.IsOpen);
            for (int i = 0; i < 16; i++) console.Update();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) console.Update();
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before); yield return null;
        }
        sealed class TeleportNameOnly : IDevCommand
        {
            public string Name => "teleport";
            public string Usage => "teleport";
            public string Summary => "Completion fixture only; DEV-009 owns the actual command";
            public DevCommandResult Execute(DevArgs args, DevContext context) => throw new InvalidOperationException("Completion fixture cannot execute");
        }
    }
}
#endif
