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
        public Vector2 PanDelta => Vector2.zero; // TODO(INP-006): Editor map sampling.
        public float ZoomSteps { get; private set; }
        public bool RecenterPressed { get; private set; }
        public bool FocusPressed => false; // TODO(INP-006): Editor action edges.
        public bool ViewIsoPressed => false;
        public bool ViewTopPressed => false;
        public bool ViewFrontPressed => false;
        public bool ViewSidePressed => false;
        public void Sample()
        {
            OrbitDelta = Vector2.zero; ZoomSteps = 0; RecenterPressed = false;
            if (input.CurrentContext != InputContext.Flight) return;
            var flight = input.Controls.Flight;
            if (flight.CameraOrbitHold.IsPressed()) OrbitDelta = flight.CameraLook.ReadValue<Vector2>();
            ZoomSteps = flight.CameraZoom.ReadValue<float>() * input.CameraZoomSensitivity;
            RecenterPressed = flight.CameraRecenter.enabled && flight.CameraRecenter.WasPressedThisFrame();
        }
    }
}
