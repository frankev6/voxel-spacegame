using Game.App.EditorHost;
using Game.Core;
using Game.Core.Events;
using Game.Core.Settings;
using Game.Input;
using Game.Presentation.Data;
using Game.Presentation.Input;
using Game.ShipEditor.Data;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Fixture = Game.Tests.EditMode.Presentation.CameraRigServiceTests.Fixture;

namespace Game.Tests.EditMode.Input
{
    /// <summary>INP-006: real device events through the camera and editor adapters.</summary>
    public sealed class SamplerTests : InputTestFixture
    {
        sealed class Gate : IWorldPointerGate
        {
            public bool Allowed = true;
            public bool IsWorldPointerAllowed(Vector2 point) => Allowed;
        }
        InputService input;
        InputCameraInput camera;
        EditorInputSampler editor;
        Mouse mouse;
        Keyboard keyboard;
        Gate gate;
        [SetUp] public override void Setup()
        {
            base.Setup(); EventHub.Reset();
            InputSystem.settings.shortcutKeysConsumeInput = true;
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            input = new InputService(new ControlSettingsData());
            camera = new InputCameraInput(input); gate = new Gate(); editor = new EditorInputSampler(input, gate);
        }
        [TearDown] public override void TearDown()
        {
            try { input?.Dispose(); EventHub.Reset(); }
            finally { base.TearDown(); }
        }
        void MouseFrame(Vector2 delta = default, float scroll = 0, bool middle = false)
        {
            var state = new MouseState { delta = delta, scroll = new Vector2(0, scroll), position = new Vector2(120, 80) };
            if (middle) state = state.WithButton(MouseButton.Middle);
            InputSystem.QueueStateEvent(mouse, state); InputSystem.Update(); camera.Sample();
        }
        [Test] public void Orbit_OnlyWhileHeld()
        {
            foreach (var context in new[] { InputContext.Flight, InputContext.Editor })
            {
                input.SetContext(context);
                MouseFrame(new Vector2(8, 12)); Assert.AreEqual(Vector2.zero, camera.OrbitDelta);
                MouseFrame(new Vector2(8, 12), middle: true); Assert.AreEqual(new Vector2(8, 12), camera.OrbitDelta);
                MouseFrame(new Vector2(8, 12)); Assert.AreEqual(Vector2.zero, camera.OrbitDelta);
            }
            Press(keyboard.leftShiftKey); MouseFrame(new Vector2(3, 7), middle: true);
            Assert.AreEqual(new Vector2(3, 7), camera.PanDelta); Assert.AreEqual(Vector2.zero, camera.OrbitDelta);
            Release(keyboard.leftShiftKey);
            Press(keyboard.fKey); camera.Sample(); Assert.IsTrue(camera.FocusPressed);
            input.SetContext(InputContext.Menu); camera.Sample();
            Assert.AreEqual(Vector2.zero, camera.OrbitDelta); Assert.AreEqual(Vector2.zero, camera.PanDelta);
            Assert.AreEqual(0, camera.ZoomSteps); Assert.IsFalse(camera.FocusPressed);
        }
        [Test] public void InvertY()
        {
            input.SetContext(InputContext.Flight); input.MouseSensitivity = 2;
            foreach (bool invert in new[] { false, true })
            {
                input.InvertCameraY = invert;
                using var f = new Fixture(input: camera);
                f.Rig.ApplySettings(new CameraSettings { MouseSensitivity = input.MouseSensitivity, InvertOrbitY = invert, ShakeScale = 1 });
                f.Rig.SetMode(CameraMode.Flight, true);
                f.Rig.SetTarget(float3.zero, quaternion.identity, float3.zero, 10, true);
                MouseFrame(); f.Rig.Tick(0);
                MouseFrame(new Vector2(0, 10), middle: true);
                Assert.AreEqual(10, camera.OrbitDelta.y, "Adapter passes raw pixels, regardless of inversion/sensitivity");
                f.Rig.Tick(1f / 60); MouseFrame();
                for (int i = 0; i < 180; i++) f.Rig.Tick(1f / 60);
                float pitch = math.degrees(math.asin(-f.Rig.Camera.transform.forward.y));
                float expected = f.Config.DefaultPitchDeg + (invert ? 1 : -1) * 10 * f.Config.OrbitDegPerPixel * 2;
                Assert.AreEqual(expected, pitch, .001f);
            }
        }
        [Test] public void CtrlScroll_ToolWheel()
        {
            input.SetContext(InputContext.Editor); input.CameraZoomSensitivity = 2;
            var frame = default(EditorInputFrame);
            MouseFrame(scroll: 120); editor.Sample(ref frame);
            Assert.AreEqual(2, camera.ZoomSteps, .001f); Assert.AreEqual(0, frame.ToolWheel);
            Assert.AreEqual(new float2(120, 80), frame.PointerScreen); Assert.IsTrue(frame.PointerAllowed);
            Press(keyboard.leftCtrlKey); MouseFrame(scroll: 120); editor.Sample(ref frame);
            Assert.AreEqual(0, camera.ZoomSteps); Assert.AreEqual(1, frame.ToolWheel, .001f); Assert.IsTrue(frame.Ctrl);
            gate.Allowed = false; editor.Sample(ref frame); Assert.IsFalse(frame.PointerAllowed);
            input.SetContext(InputContext.Menu); editor.Sample(ref frame); Assert.AreEqual(default(EditorInputFrame), frame);
        }
        [Test] public void CtrlZ_UndoOnly()
        {
            input.SetContext(InputContext.Editor); var frame = default(EditorInputFrame);
            Press(keyboard.leftCtrlKey); Press(keyboard.zKey); editor.Sample(ref frame);
            Assert.AreEqual(EditorActions.Undo, frame.Actions, "Ctrl+Z must not also mirror the selection on Z");
            InputSystem.Update(); editor.Sample(ref frame); Assert.AreEqual(EditorActions.None, frame.Actions, "Held shortcuts have no new edge");
            Release(keyboard.zKey); Release(keyboard.leftCtrlKey); Press(keyboard.zKey); editor.Sample(ref frame);
            Assert.AreEqual(EditorActions.MirrorZ, frame.Actions, "Plain Z still selects the mirror action");
        }
    }
}
