using System;
using Game.Presentation.Data;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Presentation.Services
{
    /// <summary>Owns the app camera and presentation pose (SPEC-62 §5.2/§6, ADR-0039).</summary>
    public sealed class CameraRigService : IDisposable
    {
        static readonly ProfilerMarker TickMarker = new ProfilerMarker(ProfilerCategory.Scripts, "CameraRigService.Tick");
        readonly CameraConfig config;
        readonly ICameraInput input;
        GameObject instance;
        Camera camera;
        Transform listenerPivot;
        CameraMode mode;
        CameraSettings settings = new CameraSettings { MouseSensitivity = 1, ShakeScale = 1 };
        bool blending, disposed;
        float blendT, yawDeg;
        float3 blendFromPos, outPos;
        quaternion blendFromRot, outRot;
        internal CameraConfig Config => config;
        internal bool HasTarget { get; private set; }
        internal float3 TargetPosition { get; private set; }
        internal quaternion TargetRotation { get; private set; }
        internal float3 TargetVelocity { get; private set; }
        internal float TargetRadius { get; private set; }
        internal bool CutRequested { get; private set; }
        internal float3 FocusState { get; private set; }

        public CameraRigService(CameraConfig config, Transform parent, ICameraInput input)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
            this.input = input;
            if (!math.isfinite(config.MinPitchDeg) || config.MinPitchDeg < 20)
                throw new InvalidOperationException("CameraConfig.MinPitchDeg must be at least 20 degrees.");
            if (config.CameraPrefab == null) throw new InvalidOperationException("CameraConfig.CameraPrefab is missing.");
            try
            {
                instance = Object.Instantiate(config.CameraPrefab, parent, false);
                var cameras = instance.GetComponentsInChildren<Camera>(true);
                if (cameras.Length != 1) throw new InvalidOperationException("Camera prefab must contain exactly one Camera.");
                if (instance.GetComponentsInChildren<AudioListener>(true).Length != 0)
                    throw new InvalidOperationException("AudioService owns the listener; the camera prefab must not contain one.");
                camera = cameras[0]; camera.fieldOfView = config.VerticalFovDeg;
                camera.nearClipPlane = config.NearClip; camera.farClipPlane = config.FarClip;
                listenerPivot = new GameObject("ListenerPivot").transform;
                listenerPivot.SetParent(instance.transform, false);
                mode = CameraMode.Menu; FocusState = config.MenuLookAt;
                MenuPose(out outPos, out outRot);
                ApplyPose();
            }
            catch { Dispose(); throw; }
        }

        public Camera Camera => camera;
        public Transform ListenerPivot => listenerPivot;
        public CameraMode Mode => mode;
        public float3 Focus => FocusState;
        public float CurrentDistance => config.DefaultDistance; // TODO(CAM-005): damped flight distance.
        public float YawDeg => yawDeg;

        public void SetMode(CameraMode mode, bool cut)
        {
            if (disposed) return;
            this.mode = mode;
            blending = !cut && !settings.ReduceMotion && config.BlendSeconds > 0;
            blendT = 0; blendFromPos = outPos; blendFromRot = outRot;
            // TODO(CAM-005): entering Flight resets pitch/distance targets and yaw offset.
            // TODO(CAM-009): clear editor input suppression on mode changes.
        }

        public void ApplySettings(in CameraSettings settings)
        {
            if (disposed) return;
            this.settings = settings;
            this.settings.MouseSensitivity = math.isfinite(settings.MouseSensitivity) ? math.clamp(settings.MouseSensitivity, .1f, 3) : 1;
            this.settings.ShakeScale = math.isfinite(settings.ShakeScale) ? math.saturate(settings.ShakeScale) : 1;
            if (this.settings.ReduceMotion) blending = false;
        }

        public void SetTarget(float3 pos, quaternion rot, float3 vel, float radius, bool cut)
        {
            if (disposed) return;
            CutRequested |= cut || !HasTarget; HasTarget = true;
            TargetPosition = pos; TargetRotation = rot; TargetVelocity = vel; TargetRadius = radius;
            if (CutRequested) FocusState = pos;
        }
        public void ClearTarget() { HasTarget = false; CutRequested = false; }
        public void Warp(float3 delta) => throw new NotImplementedException("CAM-006");
        public void AddImpulse(float3 worldPos, float strength, float radius) => throw new NotImplementedException("CAM-008");
        public float3 DesiredFlightPosition() => throw new NotImplementedException("CAM-005");
        public void SetOcclusionDistance(float distance) => throw new NotImplementedException("CAM-007");
        public void EnterEditorOrbit(float3 center, float radius, bool cut) => throw new NotImplementedException("CAM-009");
        public void FocusOn(float3 center, float radius) => throw new NotImplementedException("CAM-009");
        public void SnapView(EditorView view) => throw new NotImplementedException("CAM-009");
        public void SetEditorSuppression(bool zoom, bool orbit) => throw new NotImplementedException("CAM-009");
        public Ray ScreenPointToRay(Vector2 screenPosBottomLeft) => camera.ScreenPointToRay(screenPosBottomLeft);

        public void Tick(float dt)
        {
            if (disposed) return;
            using var marker = TickMarker.Auto();
            dt = math.isfinite(dt) ? math.clamp(dt, 0, .1f) : 0;
            input?.Sample();
            float3 p = outPos; quaternion r = outRot;
            if (mode == CameraMode.Menu)
            {
                if (!settings.ReduceMotion) yawDeg += config.MenuDriftDegPerSecond * dt;
                MenuPose(out p, out r);
            }
            // Hold and targetless Flight retain the last output; CAM-005/CAM-009 supply their mode poses.
            if (blending)
            {
                blendT = math.min(1, blendT + dt / config.BlendSeconds);
                float w = math.smoothstep(0, 1, blendT);
                p = math.lerp(blendFromPos, p, w); r = math.slerp(blendFromRot, r, w);
                if (blendT >= 1) blending = false;
            }
            // TODO(CAM-008): additive shake after the logical mode/blend pose.
            outPos = p; outRot = r;
            ApplyPose();
        }

        void MenuPose(out float3 position, out quaternion rotation)
        {
            float3 center = config.MenuLookAt;
            position = center + math.rotate(quaternion.RotateY(math.radians(yawDeg)), (float3)config.MenuPosition - center);
            rotation = quaternion.LookRotationSafe(center - position, math.up());
        }
        void ApplyPose()
        {
            camera.transform.SetPositionAndRotation(outPos, outRot);
            listenerPivot.SetPositionAndRotation(mode == CameraMode.Flight && HasTarget ? TargetPosition : outPos, outRot);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (instance != null)
            {
                if (Application.isPlaying) Object.Destroy(instance); else Object.DestroyImmediate(instance);
            }
            instance = null; camera = null; listenerPivot = null;
        }
    }
}
