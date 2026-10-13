using System.Linq;
using Game.Core;
using Game.Core.Events;
using Game.Core.Settings;
using Game.Input;
using Game.Presentation.Components;
using Game.Presentation.Input;
using Game.Simulation.Commands;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Tests.EditMode.Input
{
    /// <summary>INP-008: scheme coverage and behavior through synthetic Input System device events.</summary>
    public sealed class GamepadTests : InputTestFixture
    {
        sealed class BlockedPointer : IWorldPointerGate
        { public bool IsWorldPointerAllowed(Vector2 point) => false; }
        InputService input;
        Gamepad pad;
        Keyboard keyboard;
        Mouse mouse;
        World world;
        PlayerInputSampleSystem sampler;
        EntityQuery commands;
        Entity player;
        readonly float3 startPosition = new float3(20, 4, 30);

        [SetUp] public override void Setup()
        {
            base.Setup(); EventHub.Reset();
            InputSystem.settings.shortcutKeysConsumeInput = true;
            pad = InputSystem.AddDevice<Gamepad>();
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            input = new InputService(new ControlSettingsData()); input.SetContext(InputContext.Flight);
            world = new World("GamepadTests"); sampler = world.GetOrCreateSystemManaged<PlayerInputSampleSystem>();
            sampler.Bind(input, new BlockedPointer()); commands = world.EntityManager.CreateEntityQuery(typeof(PlayerCommand));
            player = world.EntityManager.CreateEntity(typeof(PlayerShip), typeof(LocalTransform));
            world.EntityManager.SetComponentData(player, LocalTransform.FromPositionRotationScale(startPosition, quaternion.identity, 1));
        }
        [TearDown] public override void TearDown()
        {
            try { world?.Dispose(); input?.Dispose(); EventHub.Reset(); }
            finally { base.TearDown(); }
        }
        PlayerCommand Command => commands.GetSingleton<PlayerCommand>();
        void PadFrame(GamepadState state)
        { InputSystem.QueueStateEvent(pad, state); InputSystem.Update(); sampler.Update(); }
        void AssertAim(float3 expected)
        { Assert.AreEqual(1, Command.AimValid); Assert.Less(math.distance(expected, Command.AimWorld), .001f); }
        string PadDisplay(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
                if (!action.bindings[i].isPartOfComposite && (action.bindings[i].groups ?? "").Split(InputBinding.Separator).Contains("Gamepad"))
                    return action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            Assert.Fail(action.name + " has no Gamepad binding"); return null;
        }

        [Test] public void AllFlightActionsBound()
        {
            Assert.AreEqual("<Gamepad>", input.Controls.GamepadScheme.deviceRequirements[0].controlPath);
            Assert.IsFalse(input.Controls.GamepadScheme.deviceRequirements[0].isOptional);
            foreach (var map in new[] { input.Controls.Flight.Get(), input.Controls.Menu.Get(), input.Controls.Dialog.Get() })
                foreach (var action in map.actions)
                {
                    Assert.IsTrue(action.bindings.Any(b => !b.isComposite && (b.groups ?? "").Split(InputBinding.Separator).Contains("Gamepad")), map.name + "/" + action.name);
                    Assert.IsTrue(action.controls.Any(c => c.device == pad), map.name + "/" + action.name + " must resolve to the device");
                    Assert.IsNotEmpty(PadDisplay(action));
                }
            // No camera singleton is needed for directional aim; pointer blocking does not block triggers.
            PadFrame(new GamepadState { leftStick = new Vector2(.5f, -.75f), rightStick = new Vector2(1, 1), leftTrigger = 1, rightTrigger = 1 }.WithButton(GamepadButton.RightShoulder));
            Assert.AreEqual(pad.leftStick.x.ReadValue(), Command.Thrust.x, .001f);
            Assert.AreEqual(pad.leftStick.y.ReadValue(), Command.Thrust.y, .001f);
            Assert.AreEqual(3u, Command.FireGroups); Assert.AreEqual(ShipCommandFlags.Boost, Command.Flags);
            float3 diagonal = math.normalize(new float3(1, 0, 1)); AssertAim(startPosition + diagonal * 50);
            var moved = startPosition + new float3(7, 0, -5);
            world.EntityManager.SetComponentData(player, LocalTransform.FromPositionRotationScale(moved, quaternion.identity, 1));
            PadFrame(default); AssertAim(moved + diagonal * 50);
            PadFrame(new GamepadState().WithButton(GamepadButton.LeftShoulder).WithButton(GamepadButton.DpadRight));
            Assert.AreEqual(ShipCommandFlags.Brake, Command.Flags); Assert.AreEqual(1, Command.Turn);
            PadFrame(default);
            Press(pad.selectButton); Press(pad.buttonSouth); sampler.Update();
            Assert.AreEqual(1, Command.Edges.Ability1); Assert.AreEqual(0, Command.Edges.Interact, "A modified ability must not also interact");
            Release(pad.buttonSouth); Release(pad.selectButton);
            Press(pad.startButton); Press(pad.buttonSouth); sampler.Update();
            Assert.IsTrue(input.Controls.Flight.OpenInventory.WasPressedThisFrame());
            Assert.AreEqual(0, Command.Edges.Interact, "A screen chord must not also interact");
            Release(pad.buttonSouth); Release(pad.startButton);
            Set(pad.rightStick, new Vector2(0, 1)); Press(pad.rightStickButton);
            var cameraInput = new InputCameraInput(input); cameraInput.Sample();
            Assert.AreEqual(pad.rightStick.ReadValue(), cameraInput.OrbitDelta);
            Release(pad.rightStickButton); PadFrame(default);
            input.SetContext(InputContext.Menu);
            Press(pad.startButton); Press(pad.buttonWest); sampler.Update();
            Assert.IsTrue(input.Controls.Menu.OpenGalaxyMap.WasPressedThisFrame());
            Assert.AreEqual(float2.zero, Command.Thrust); Assert.AreEqual(0u, Command.FireGroups); Assert.AreEqual(0, Command.AimValid);
            Release(pad.buttonWest); Release(pad.startButton);
            input.SetContext(InputContext.Dialog); Press(pad.dpad.left);
            Assert.IsTrue(input.Controls.Dialog.Choice8.WasPressedThisFrame()); Release(pad.dpad.left);
            Press(pad.rightTrigger); Assert.IsTrue(input.Controls.Dialog.Continue.WasPressedThisFrame()); Release(pad.rightTrigger);
            Press(pad.selectButton); Press(pad.startButton); Assert.IsTrue(input.Controls.Global.Back.WasPressedThisFrame());
            Release(pad.startButton); Release(pad.selectButton);
            input.SetContext(InputContext.Flight);
            var inverse = new float4x4(new float4(10, 0, 0, 0), new float4(0, 0, 10, 0), new float4(0, -100, 0, 0), new float4(20, 100, 30, 1));
            world.EntityManager.CreateSingleton(new MainCameraState { ScreenSizePx = new float2(200), ViewProjection = math.inverse(inverse) });
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(100, 100) }); InputSystem.Update(); sampler.Update();
            AssertAim(new float3(20, 0, 30));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.LeftShift, Key.Space)); InputSystem.Update(); sampler.Update();
            Assert.AreEqual(new float2(1, 1), Command.Thrust); Assert.AreEqual(0, Command.Turn);
            Assert.AreEqual(1u, Command.FireGroups); Assert.AreEqual(ShipCommandFlags.None, Command.Flags);
            // Changing controlled ship resets the retained direction; a missing ship leaves aim invalid.
            world.EntityManager.DestroyEntity(player);
            player = world.EntityManager.CreateEntity(typeof(PlayerShip), typeof(LocalTransform));
            world.EntityManager.SetComponentData(player, LocalTransform.FromPositionRotationScale(startPosition, quaternion.identity, 1));
            Press(pad.buttonEast); sampler.Update(); AssertAim(startPosition + new float3(0, 0, 50));
            world.EntityManager.DestroyEntity(player); sampler.Update(); Assert.AreEqual(0, Command.AimValid);
            sampler.Unbind(); sampler.Update(); Assert.AreEqual(0u, Command.FireGroups);
        }

        [Test] public void DisplaySwitchesWithDevice()
        {
            int changes = 0; input.BindingsChanged += () => changes++;
            Assert.AreEqual("F", input.GetBindingDisplay("Flight/Interact"));
            PadFrame(new GamepadState { rightStick = new Vector2(.01f, 0) });
            Assert.AreEqual("KeyboardMouse", input.CurrentControlScheme, "Drift must not steal the scheme");
            Press(pad.buttonSouth);
            Assert.AreEqual("Gamepad", input.CurrentControlScheme); Assert.AreEqual(1, changes);
            Assert.AreEqual(PadDisplay(input.Controls.Flight.Interact), input.GetBindingDisplay("Flight/Interact"));
            Assert.AreSame(input.GetBindingDisplay("Flight/Interact"), input.GetBindingDisplay("Flight/Interact"));
            Assert.AreEqual(PadDisplay(input.Controls.Flight.Ability1), input.GetBindingDisplay("Flight/Ability1"), "Display includes the complete chord");
            Assert.AreEqual("F", input.GetBindingDisplay("Editor/Focus"), "Keyboard-only actions retain a usable label");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            Assert.AreEqual("Gamepad", input.CurrentControlScheme, "An idle keyboard packet must not steal the scheme");
            Press(keyboard.fKey); Assert.AreEqual("KeyboardMouse", input.CurrentControlScheme); Assert.AreEqual(2, changes);
            Assert.AreEqual("F", input.GetBindingDisplay("Flight/Interact"));
            Release(pad.buttonSouth); Assert.AreEqual("KeyboardMouse", input.CurrentControlScheme, "A gamepad release must not steal the scheme");
            Set(pad.rightStick, Vector2.right); Assert.AreEqual("Gamepad", input.CurrentControlScheme);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(40, 70) }); InputSystem.Update();
            Assert.AreEqual("KeyboardMouse", input.CurrentControlScheme); Assert.AreEqual("F", input.GetBindingDisplay("Flight/Interact"));
            Set(pad.rightStick, Vector2.up); Assert.AreEqual("Gamepad", input.CurrentControlScheme);
            InputSystem.RemoveDevice(pad); Assert.AreEqual("KeyboardMouse", input.CurrentControlScheme);
            Assert.AreEqual("F", input.GetBindingDisplay("Flight/Interact"));
            input.Dispose(); int disposedChanges = changes;
            Press(keyboard.spaceKey); Assert.AreEqual(disposedChanges, changes, "Disposed services unsubscribe from device events");
        }
    }
}
