using System.Collections;
using Game.App.Services;
using Game.Core;
using Game.Core.Settings;
using Game.Input;
using Game.Presentation.Input;
using Game.Simulation.Commands;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.Views;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.Input
{
    /// <summary>INP-004: held input crosses real navigation, pause, input bridge and ECS sampling (SPEC-03 §9).</summary>
    public sealed class ContextIntegrationTests
    {
        sealed class FlightScreen : UIScreen
        {
            public override string Key => "input-test-flight";
            public override UILayer Layer => UILayer.Hud;
            public override InputContext Context => InputContext.Flight;
            public override bool IsRoot => true;
            public override bool PausesSimulation => false;
            protected override void OnBuild() { }
        }
        sealed class PauseScreen : UIScreen
        {
            public override string Key => "input-test-pause";
            public override UILayer Layer => UILayer.Modals;
            public override InputContext Context => InputContext.Menu;
            public override bool PausesSimulation => true;
            protected override void OnBuild() { }
        }

        InputSettings.UpdateMode previousUpdateMode;
        Keyboard keyboard;
        Mouse mouse;
        InputService input;
        InputServiceBridge bridge;
        PauseService pause;
        UINavigator navigator;
        World world;
        PlayerInputSampleSystem sampler;
        EntityQuery commands;

        [SetUp]
        public void SetUp()
        {
            previousUpdateMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            keyboard = InputSystem.AddDevice<Keyboard>("ContextIntegrationKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("ContextIntegrationMouse");
            input = new InputService(new ControlSettingsData());
            input.Controls.asset.devices = new InputDevice[] { keyboard, mouse };
            bridge = new InputServiceBridge(input);
            pause = new PauseService();
            navigator = new UINavigator(pause, bridge, new UnityFrameClock(), new VisualElement());
            bridge.BindBack(navigator.Back);
            var flight = new FlightScreen();
            navigator.Register(flight);
            navigator.SetRoot(flight);
            world = new World("ContextIntegrationTests");
            sampler = world.GetOrCreateSystemManaged<PlayerInputSampleSystem>();
            sampler.Bind(input, null);
            commands = world.EntityManager.CreateEntityQuery(typeof(PlayerCommand));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            sampler?.Unbind();
            world?.Dispose();
            navigator?.DisposeOwnedScreens();
            bridge?.Dispose();
            input?.Dispose();
            pause?.Dispose();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = previousUpdateMode;
            yield return null;
        }

        PlayerCommand Command => commands.GetSingleton<PlayerCommand>();
        void InputFrame(params Key[] held)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
            InputSystem.Update();
            sampler.Update();
        }

        [UnityTest]
        public IEnumerator HeldKey_AcrossPause()
        {
            // Establish a real counted press so the test detects clearing as well as phantom increments.
            InputFrame(Key.W, Key.F);
            Assert.AreEqual(1f, Command.Thrust.y);
            Assert.AreEqual(1, Command.Edges.Interact);
            yield return null;
            InputFrame(Key.W);
            var before = Command.Edges;
            var menu = new PauseScreen();
            navigator.Register(menu);
            int openingFrame = Time.frameCount;
            navigator.Push(menu);

            Assert.AreEqual(openingFrame, Time.frameCount);
            Assert.AreEqual(InputContext.Menu, input.CurrentContext);
            Assert.AreEqual(0f, input.Controls.Flight.Thrust.ReadValue<float>(), "Map disables immediately");
            Assert.IsTrue(pause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            sampler.Update();
            Assert.AreEqual(0f, Command.Thrust.y, "Same-frame sampling must be neutral");
            Assert.AreEqual(before, Command.Edges);

            yield return null;
            InputFrame(Key.W);
            Assert.AreEqual(0f, Command.Thrust.y);
            Assert.AreEqual(before, Command.Edges);

            // Close through Global.Back -> InputServiceBridge -> navigator, keeping W physically held.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Escape));
            InputSystem.Update();
            Assert.IsFalse(menu.IsOpen);
            Assert.IsFalse(pause.IsPaused);
            Assert.AreEqual(InputContext.Flight, input.CurrentContext);
            // Value actions perform their initial-state check on the next input update after Enable.
            InputSystem.Update();
            sampler.Update();
            Assert.AreEqual(1f, input.Controls.Flight.Thrust.ReadValue<float>());
            Assert.AreEqual(1f, Command.Thrust.y);
            Assert.AreEqual(before, Command.Edges, "Resuming must not invent or clear an action edge");

            yield return null;
            InputFrame(Key.W);
            Assert.AreEqual(1f, Command.Thrust.y);
            Assert.AreEqual(before, Command.Edges);
        }
    }
}
