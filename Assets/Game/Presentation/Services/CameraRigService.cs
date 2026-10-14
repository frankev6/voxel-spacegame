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
        float blendT, yawDeg, yawOffsetDeg, pitchDeg, pitchTargetDeg, distance, distanceTarget;
        float occlusionDistance = float.PositiveInfinity, effectiveDistance;
        bool occlusionRecovering;
        float3 lookAhead, previousFollowPosition, framingOffset;
        float3 boundsMin, boundsMax;
        float4x4 boundsTransform;
        bool hasBounds;
        float3 pivot, pivotTarget, boundsCenter;
        float boundsRadius, edYaw, edYawTarget, edPitch, edPitchTarget, edDistance, edDistanceTarget;
        bool suppressZoom, suppressOrbit;
        float3 blendFromPos, outPos;
        quaternion blendFromRot, outRot;
        float3 unshakenPos;
        quaternion unshakenRot;
        float trauma, shakeTime;
        internal CameraConfig Config => config;
        internal bool HasTarget { get; private set; }
        internal float3 TargetPosition { get; private set; }
        internal quaternion TargetRotation { get; private set; }
        internal float3 TargetVelocity { get; private set; }
        internal float TargetRadius { get; private set; }
        internal bool CutRequested { get; private set; }
        internal float3 FocusState { get; private set; }
        internal float Trauma => trauma;

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
                pitchDeg = pitchTargetDeg = config.DefaultPitchDeg; distance = distanceTarget = config.DefaultDistance; effectiveDistance = distance;
                MenuPose(out outPos, out outRot);
                unshakenPos = outPos; unshakenRot = outRot;
                ApplyPose();
            }
            catch { Dispose(); throw; }
        }

        public Camera Camera => camera;
        public Transform ListenerPivot => listenerPivot;
        public CameraMode Mode => mode;
        public float3 Focus => mode == CameraMode.EditorOrbit ? pivot : FocusState + framingOffset;
        public float CurrentDistance => mode == CameraMode.EditorOrbit ? edDistance : distance;
        internal float3 LookAhead => lookAhead;
        public float YawDeg => mode == CameraMode.EditorOrbit ? edYaw : yawDeg;

        public void SetMode(CameraMode mode, bool cut)
        {
            if (disposed) return;
            this.mode = mode; occlusionDistance = float.PositiveInfinity; occlusionRecovering = false;
            blending = !cut && !settings.ReduceMotion && config.BlendSeconds > 0;
            blendT = 0; blendFromPos = outPos; blendFromRot = outRot;
            if (mode == CameraMode.Flight)
            {
                pitchTargetDeg = config.DefaultPitchDeg; distanceTarget = config.DefaultDistance; yawOffsetDeg = 0;
                if (cut || settings.ReduceMotion) CutRequested = true;
            }
            suppressZoom = suppressOrbit = false;
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
            TargetPosition = pos; TargetRotation = rot; TargetVelocity = vel; TargetRadius = radius; hasBounds = false;
            if (CutRequested) { FocusState = pos; framingOffset = 0; blending = false; }
        }
        internal void SetTargetBounds(float3 min, float3 max, float4x4 localToWorld)
        { boundsMin = min; boundsMax = max; boundsTransform = localToWorld; hasBounds = true; }
        public void ClearTarget() { HasTarget = false; CutRequested = false; }
        public void Warp(float3 delta)
        {
            if (disposed) return;
            FocusState += delta; TargetPosition += delta;
            pivot += delta; pivotTarget += delta; boundsCenter += delta;
            blendFromPos += delta; previousFollowPosition += delta;
            outPos += delta; unshakenPos += delta;
            if (hasBounds) boundsTransform.c3.xyz += delta;
            // Directions, framing displacement, damping and blend progress remain in the same relative state.
            ApplyPose();
        }
        public void AddImpulse(float3 worldPos, float strength, float radius)
        {
            if (disposed || !math.all(math.isfinite(worldPos)) || !math.isfinite(strength) || !math.isfinite(radius) || strength <= 0 || radius <= 0) return;
            float falloff = math.saturate(1 - math.distance(worldPos, Focus) / radius);
            trauma = math.saturate(trauma + strength * falloff);
        }
        public float3 DesiredFlightPosition() => CameraMath.OrbitPosition(Focus, CameraMath.OrbitRotation(pitchDeg, yawDeg), distance);
        public void SetOcclusionDistance(float distance) => occlusionDistance = math.isnan(distance) ? float.PositiveInfinity : math.max(0, distance);
        public void EnterEditorOrbit(float3 center, float radius, bool cut)
        {
            if (disposed) return;
            boundsCenter = center; boundsRadius = math.max(radius, 1);
            pivotTarget = center; edYawTarget = 45; edPitchTarget = 30;
            edDistanceTarget = math.clamp(radius * config.EditorDistanceRadiusFactor + config.EditorDistancePad, config.EditorMinDistance, MaxEditorDistance);
            SetMode(CameraMode.EditorOrbit, cut);
            if (cut || settings.ReduceMotion)
            {
                pivot = pivotTarget; edYaw = edYawTarget; edPitch = edPitchTarget; edDistance = edDistanceTarget;
            }
        }
        public void FocusOn(float3 center, float radius)
        {
            if (disposed) return;
            pivotTarget = center;
            edDistanceTarget = math.clamp(radius * config.EditorFocusPadding / math.sin(math.radians(config.VerticalFovDeg * .5f)), config.EditorMinDistance, MaxEditorDistance);
        }
        public void SnapView(EditorView view)
        {
            if (disposed) return;
            switch (view)
            {
                case EditorView.Iso: edYawTarget = 45; edPitchTarget = 30; break;
                case EditorView.Top: edYawTarget = edYaw; edPitchTarget = 89; break;
                case EditorView.Front: edYawTarget = 0; edPitchTarget = 0; break;
                case EditorView.Side: edYawTarget = 90; edPitchTarget = 0; break;
                default: throw new ArgumentOutOfRangeException(nameof(view));
            }
            edPitchTarget = math.clamp(edPitchTarget, config.EditorMinPitchDeg, config.EditorMaxPitchDeg);
        }
        public void SetEditorSuppression(bool zoom, bool orbit)
        {
            if (disposed) return;
            suppressZoom = zoom; suppressOrbit = orbit;
        }
        float MaxEditorDistance => boundsRadius * config.EditorMaxDistanceRadiusFactor + config.EditorMaxDistancePad;
        public Ray ScreenPointToRay(Vector2 screenPosBottomLeft) => camera.ScreenPointToRay(screenPosBottomLeft);

        public void Tick(float dt)
        {
            if (disposed) return;
            using var marker = TickMarker.Auto();
            dt = math.isfinite(dt) ? math.clamp(dt, 0, .1f) : 0;
            input?.Sample();
            float3 p = unshakenPos; quaternion r = unshakenRot;
            if (mode == CameraMode.Menu)
            {
                framingOffset = 0;
                if (!settings.ReduceMotion) yawDeg += config.MenuDriftDegPerSecond * dt;
                MenuPose(out p, out r);
            }
            else if (mode == CameraMode.Flight && HasTarget) FlightPose(dt, out p, out r);
            else if (mode == CameraMode.EditorOrbit) EditorPose(dt, out p, out r);
            // Hold/targetless Flight keep the logical pose so additive shake never accumulates into a drift.
            if (blending)
            {
                blendT = math.min(1, blendT + dt / config.BlendSeconds);
                float w = math.smoothstep(0, 1, blendT);
                p = math.lerp(blendFromPos, p, w); r = math.slerp(blendFromRot, r, w);
                if (blendT >= 1) blending = false;
            }
            unshakenPos = p; unshakenRot = r;
            trauma = math.max(0, trauma - config.TraumaDecayPerSecond * dt);
            shakeTime += dt;
            float amount = trauma * trauma * settings.ShakeScale;
            outPos = p; outRot = r;
            if (amount > 0 && !settings.ReduceMotion)
            {
                float t = shakeTime * config.ShakeFrequencyHz;
                float3 offset = new float3(noise.snoise(new float2(t, 0)), noise.snoise(new float2(t, 10)), 0)
                    * config.ShakeMaxOffsetFraction * CurrentDistance * amount;
                float3 angles = new float3(noise.snoise(new float2(t, 20)), noise.snoise(new float2(t, 30)), noise.snoise(new float2(t, 40)) * .5f)
                    * config.ShakeMaxAngleDeg * amount;
                outPos += math.rotate(r, offset);
                outRot = math.mul(r, quaternion.EulerXYZ(math.radians(angles)));
            }
            ApplyPose();
        }

        void EditorPose(float dt, out float3 position, out quaternion rotation)
        {
            Vector2 orbit = input?.OrbitDelta ?? Vector2.zero;
            float k = config.OrbitDegPerPixel * settings.MouseSensitivity;
            if (!suppressOrbit)
            {
                edYawTarget += orbit.x * k;
                edPitchTarget = math.clamp(edPitchTarget - orbit.y * k * (settings.InvertOrbitY ? -1 : 1), config.EditorMinPitchDeg, config.EditorMaxPitchDeg);
            }
            var currentRotation = CameraMath.OrbitRotation(edPitch, edYaw);
            Vector2 pan = input?.PanDelta ?? Vector2.zero;
            float3 right = math.rotate(currentRotation, new float3(1, 0, 0)), up = math.rotate(currentRotation, new float3(0, 1, 0));
            pivotTarget += (-right * pan.x - up * pan.y) * edDistance * config.EditorPanPerPixelPerMeter;
            float extent = boundsRadius + config.EditorPivotBoundsPad;
            pivotTarget = math.clamp(pivotTarget, boundsCenter - extent, boundsCenter + extent);
            if (!suppressZoom)
                edDistanceTarget = math.clamp(edDistanceTarget * math.pow(config.ZoomStepFactor, input?.ZoomSteps ?? 0), config.EditorMinDistance, MaxEditorDistance);
            if (input?.ViewIsoPressed == true) SnapView(EditorView.Iso);
            if (input?.ViewTopPressed == true) SnapView(EditorView.Top);
            if (input?.ViewFrontPressed == true) SnapView(EditorView.Front);
            if (input?.ViewSidePressed == true) SnapView(EditorView.Side);
            if (input?.FocusPressed == true) FocusOn(boundsCenter, boundsRadius);
            edYaw = CameraMath.DampAngle(edYaw, edYawTarget, config.EditorHalfLife, dt);
            edPitch = CameraMath.Damp(edPitch, edPitchTarget, config.EditorHalfLife, dt);
            edDistance = CameraMath.Damp(edDistance, edDistanceTarget, config.EditorHalfLife, dt);
            pivot = CameraMath.Damp(pivot, pivotTarget, config.EditorHalfLife, dt);
            rotation = CameraMath.OrbitRotation(edPitch, edYaw);
            position = CameraMath.OrbitPosition(pivot, rotation, edDistance);
        }

        void FlightPose(float dt, out float3 position, out quaternion rotation)
        {
            Vector2 orbit = input?.OrbitDelta ?? Vector2.zero;
            float k = config.OrbitDegPerPixel * settings.MouseSensitivity;
            yawOffsetDeg += orbit.x * k;
            pitchTargetDeg = math.clamp(pitchTargetDeg - orbit.y * k * (settings.InvertOrbitY ? -1 : 1), config.MinPitchDeg, config.MaxPitchDeg);
            distanceTarget = math.clamp(distanceTarget * math.pow(config.ZoomStepFactor, input?.ZoomSteps ?? 0), config.MinDistance, config.MaxDistance);
            if (input?.RecenterPressed == true) { yawOffsetDeg = 0; pitchTargetDeg = config.DefaultPitchDeg; }
            float desiredYaw = (settings.YawMode == CameraYawMode.FollowHeading ? CameraMath.HeadingYawDeg(TargetRotation) : 0) + yawOffsetDeg;
            if (CutRequested)
            {
                yawDeg = desiredYaw; pitchDeg = pitchTargetDeg; distance = distanceTarget; effectiveDistance = distance; occlusionRecovering = false;
                lookAhead = 0; FocusState = TargetPosition; CutRequested = false;
            }
            else
            {
                yawDeg = CameraMath.DampAngle(yawDeg, desiredYaw, config.YawHalfLife, dt);
                pitchDeg = CameraMath.Damp(pitchDeg, pitchTargetDeg, config.PitchHalfLife, dt);
                distance = CameraMath.Damp(distance, distanceTarget, config.ZoomHalfLife, dt);
                float3 targetLookAhead = TargetVelocity * config.LookAheadSeconds; targetLookAhead.y = 0;
                targetLookAhead = CameraMath.ClampLength(targetLookAhead, config.LookAheadMaxFraction * distance);
                UpdateFollowFocus(targetLookAhead, dt);
            }
            previousFollowPosition = TargetPosition;
            rotation = CameraMath.OrbitRotation(pitchDeg, yawDeg);
            float limit = math.min(distance, occlusionDistance);
            if (limit < distance) occlusionRecovering = true;
            if (!occlusionRecovering || limit <= effectiveDistance) effectiveDistance = limit;
            else effectiveDistance = CameraMath.Damp(effectiveDistance, limit, config.OcclusionReleaseHalfLife, dt);
            if (math.abs(effectiveDistance - distance) < .0001f) { effectiveDistance = distance; occlusionRecovering = false; }
            framingOffset = FrameFlightFocus(rotation, effectiveDistance) - FocusState;
            position = CameraMath.OrbitPosition(Focus, rotation, effectiveDistance);
        }

        // SPEC-62 §5.3/Q15: clip only the output translation, leaving the follow filter and player input intact.
        float3 FrameFlightFocus(quaternion rotation, float effective)
        {
            var min = hasBounds ? boundsMin : new float3(-math.max(0, TargetRadius));
            var max = hasBounds ? boundsMax : -min;
            var transform = hasBounds ? boundsTransform : float4x4.Translate(TargetPosition);
            float3 center = math.transform(transform, (min + max) * .5f);
            var inverse = math.inverse(rotation);
            float3 translation = math.rotate(inverse, FocusState - center);
            float tanY = math.tan(math.radians(camera.fieldOfView * .5f)), tanX = tanY * camera.aspect;
            float fraction = 1;
            for (int i = 0; i < 8; i++)
            {
                float3 corner = new float3((i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                float3 v = math.rotate(inverse, math.transform(transform, corner) - center);
                v.z += effective;
                if (!Clip(v.z * tanX - v.x, translation.x - translation.z * tanX, ref fraction)
                    || !Clip(v.z * tanX + v.x, -translation.x - translation.z * tanX, ref fraction)
                    || !Clip(v.z * tanY - v.y, translation.y - translation.z * tanY, ref fraction)
                    || !Clip(v.z * tanY + v.y, -translation.y - translation.z * tanY, ref fraction)
                    || !Clip(v.z - camera.nearClipPlane, -translation.z, ref fraction)
                    || !Clip(camera.farClipPlane - v.z, translation.z, ref fraction)) return center;
            }
            return center + fraction * (FocusState - center);
        }
        static bool Clip(float margin, float slope, ref float fraction)
        {
            if (margin < 0) return false;
            if (slope < 0) fraction = math.min(fraction, margin / -slope);
            return true;
        }

        void UpdateFollowFocus(float3 targetLookAhead, float dt)
        {
            if (dt <= 0) return;
            // SPEC-62 Q13: exact exponential cascade for a linearly interpolated target
            // and a constant look-ahead goal during this frame. Endpoint-only lerp adds FPS-dependent lag.
            double kf = Math.Log(2) / config.FocusHalfLife, kl = Math.Log(2) / config.LookAheadHalfLife;
            double x = kf * dt, z = (kf - kl) * dt;
            double af = Math.Exp(-x), al = Math.Exp(-kl * dt);
            double ramp = x < .001 ? -x * .5 + x * x / 3 - x * x * x / 8 : af - (1 - af) / x;
            // The equal-half-life limit also avoids subtracting nearly equal exponentials.
            double cascade = Math.Abs(z) < .001
                ? af * kf * dt * (1 + z * .5 + z * z / 6 + z * z * z / 24)
                : kf * (al - af) / (kf - kl);
            FocusState = math.lerp(TargetPosition + targetLookAhead, FocusState, (float)af)
                + (TargetPosition - previousFollowPosition) * (float)ramp
                + (lookAhead - targetLookAhead) * (float)cascade;
            lookAhead = math.lerp(targetLookAhead, lookAhead, (float)al);
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
