using System;
using Game.Core;
using Game.Input;
using Game.Presentation.Services;
using UnityEngine;

namespace Game.Presentation.Input
{
    /// <summary>Context-gated camera input (SPEC-03 §5.3, SPEC-62 Q12); orbit is raw pixels.</summary>
    public sealed class InputCameraInput : ICameraInput
    {
        readonly InputService input;
        public InputCameraInput(InputService input) { this.input = input ?? throw new ArgumentNullException(nameof(input)); }
        public Vector2 OrbitDelta { get; private set; }
        public Vector2 PanDelta { get; private set; }
        public float ZoomSteps { get; private set; }
        public bool RecenterPressed { get; private set; }
        public bool FocusPressed { get; private set; }
        public bool ViewIsoPressed { get; private set; }
        public bool ViewTopPressed { get; private set; }
        public bool ViewFrontPressed { get; private set; }
        public bool ViewSidePressed { get; private set; }
        public void Sample()
        {
            OrbitDelta = PanDelta = Vector2.zero; ZoomSteps = 0; RecenterPressed = false;
            FocusPressed = ViewIsoPressed = ViewTopPressed = ViewFrontPressed = ViewSidePressed = false;
            if (input.CurrentContext == InputContext.Editor)
            {
                var editor = input.Controls.Editor;
                if (!editor.enabled) return;
                if (editor.OrbitHold.IsPressed()) OrbitDelta = editor.Orbit.ReadValue<Vector2>();
                if (editor.PanHold.IsPressed()) PanDelta = editor.Pan.ReadValue<Vector2>();
                if (!editor.ModCtrl.IsPressed()) ZoomSteps = editor.Zoom.ReadValue<float>() * input.CameraZoomSensitivity;
                FocusPressed = editor.Focus.WasPressedThisFrame();
                ViewIsoPressed = editor.ViewIso.WasPressedThisFrame();
                ViewTopPressed = editor.ViewTop.WasPressedThisFrame();
                ViewFrontPressed = editor.ViewFront.WasPressedThisFrame();
                ViewSidePressed = editor.ViewSide.WasPressedThisFrame();
                return;
            }
            if (input.CurrentContext != InputContext.Flight) return;
            var flight = input.Controls.Flight;
            if (flight.CameraOrbitHold.IsPressed()) OrbitDelta = flight.CameraLook.ReadValue<Vector2>();
            ZoomSteps = flight.CameraZoom.ReadValue<float>() * input.CameraZoomSensitivity;
            RecenterPressed = flight.CameraRecenter.enabled && flight.CameraRecenter.WasPressedThisFrame();
        }
    }
}
