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
        bool boosting;
        float fovKick;
        // ADR-0122 cruise: blend in and out of the cruise view, the spool's dip, the cruise speed and its share of the FOV.
        bool cruising, spooling, wasCruising;
        float cruiseBlend, spoolBlend, cruiseSpeed, viewSpeed, cruisePunch, cruiseFov;
        CruiseEffects cruiseEffects;
        // Feel prototype presets (FlightCameraPreset): aim feed for the `cursor` yaw and the last applied preset.
        float3 aimPoint; bool hasAim, hasPointer; float2 pointer01; float aimYawBase; int presetVersion = -1;
        static FlightCameraPreset Preset => FlightCameraPreset.Current;
        float DefaultPitch => Preset.UseConfig ? config.DefaultPitchDeg : Preset.DefaultPitchDeg;
        float MinPitch => Preset.UseConfig ? config.MinPitchDeg : Preset.MinPitchDeg;
        float MaxPitch => Preset.UseConfig ? config.MaxPitchDeg : Preset.MaxPitchDeg;
        float DefaultDistance => Preset.UseConfig ? config.DefaultDistance : Preset.DefaultDistance;
        float MinDistance => Preset.UseConfig ? config.MinDistance : Preset.MinDistance;
        float MaxDistance => Preset.UseConfig ? config.MaxDistance : Preset.MaxDistance;
        float BaseFov => Preset.UseConfig || mode != CameraMode.Flight ? config.VerticalFovDeg : Preset.FovDeg;
        float TiltUp => Preset.UseConfig ? 0 : Preset.TiltUpDeg;
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
                cruiseEffects = new CruiseEffects(instance.transform);
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
                pitchTargetDeg = DefaultPitch; distanceTarget = DefaultDistance; yawOffsetDeg = 0;
                presetVersion = FlightCameraPreset.Version; aimYawBase = yawDeg;
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
        /// <summary>QA-001: flight boost widens the field of view; reduce motion and non-flight modes keep the base FOV.</summary>
        public void SetBoost(bool active) => boosting = active;
        /// <summary>ADR-0122: the followed ship's cruise. While cruising the camera holds the ship close (no follow lag), the view widens
        /// with speed and speed streaks run past; while the drive spools the view narrows a little first.</summary>
        public void SetCruise(bool on, bool spool, float speed) { cruising = on; spooling = spool; cruiseSpeed = math.isfinite(speed) ? math.max(0, speed) : 0; }
        internal float CruiseBlend => cruiseBlend;
        /// <summary>The player's aim point this frame, for the `cursor` preset's yaw.</summary>
        public void SetAimPoint(float3 point, bool valid) { aimPoint = point; hasAim = valid && math.all(math.isfinite(point)); }
        /// <summary>The cursor as a fraction of the screen (0..1 from bottom-left), for the `cursor` preset's yaw.</summary>
        public void SetPointer(float2 fraction, bool valid) { pointer01 = fraction; hasPointer = valid && math.all(math.isfinite(fraction)); }
        internal float FovKick => fovKick;
        public void Warp(float3 delta)
        {
            if (disposed) return;
            cruiseEffects?.Warp(delta);
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
        public float3 DesiredFlightPosition() => CameraMath.OrbitPosition(Focus, CameraMath.OrbitRotation(pitchDeg, yawDeg), distance * FramingScale);
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
            float kickTarget = mode == CameraMode.Flight && boosting && !settings.ReduceMotion ? config.BoostFovKickDeg : 0;
            fovKick = settings.ReduceMotion ? 0 : CameraMath.Damp(fovKick, kickTarget, config.BoostFovHalfLife, dt);
            bool flying = mode == CameraMode.Flight && HasTarget && !settings.ReduceMotion;
            cruiseBlend = CameraMath.Damp(cruiseBlend, flying && cruising ? 1 : 0, config.CruiseHalfLife, dt);
            spoolBlend = CameraMath.Damp(spoolBlend, flying && spooling ? 1 : 0, config.CruiseHalfLife, dt);
            // the view follows an eased speed: the drive drops to a belt's cap in one tick, and the FOV and framing snapped with it
            viewSpeed = CameraMath.Damp(viewSpeed, flying && cruising ? cruiseSpeed : 0, config.CruiseSpeedHalfLife, dt);
            if (flying && cruising && !wasCruising) cruisePunch = 1;   // the drive engages: a kick
            wasCruising = flying && cruising;
            cruisePunch = CameraMath.Damp(cruisePunch, 0, config.CruisePunchHalfLife, dt);
            cruiseFov = cruiseBlend * config.CruiseFovKickDeg * math.sqrt(math.saturate(viewSpeed / math.max(1, config.CruiseFovFullSpeed)))
                - spoolBlend * config.CruiseSpoolFovDipDeg + cruisePunch * config.CruisePunchFovDeg;
            camera.fieldOfView = BaseFov + fovKick + cruiseFov;
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
            // cruise rumble: a fine angular shake that grows with speed, on the output pose only, never into the logical one
            float rumble = (cruiseBlend * math.saturate(viewSpeed / math.max(1, config.CruiseFovFullSpeed)) + cruisePunch) * config.CruiseRumbleDeg;
            if (rumble > 0 && !settings.ReduceMotion)
            {
                float t = shakeTime * config.CruiseRumbleHz;
                outRot = math.mul(outRot, quaternion.EulerXYZ(math.radians(new float3(noise.snoise(new float2(t, 50)), noise.snoise(new float2(t, 60)), 0) * rumble)));
            }
            ApplyPose();
            cruiseEffects?.Tick(cruiseBlend, viewSpeed, TargetVelocity, camera, dt, cruisePunch);
        }

        /// <summary>The cruise FOV kick is matched by moving in, so the ship keeps its size on screen and the world, not the ship, widens.</summary>
        float FramingScale
        {
            get
            {
                if (cruiseFov == 0) return 1;
                float before = math.tan(math.radians((BaseFov + fovKick) * .5f)), after = math.tan(math.radians(camera.fieldOfView * .5f));
                return before / math.max(1e-3f, after);
            }
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
            var preset = Preset;
            if (presetVersion != FlightCameraPreset.Version)
            {
                presetVersion = FlightCameraPreset.Version;
                pitchTargetDeg = DefaultPitch; distanceTarget = DefaultDistance; aimYawBase = yawDeg;
                yawOffsetDeg = preset.Yaw == FlightCameraYaw.Free ? yawDeg : 0;
            }
            Vector2 orbit = input?.OrbitDelta ?? Vector2.zero;
            float k = config.OrbitDegPerPixel * settings.MouseSensitivity;
            yawOffsetDeg += orbit.x * k;
            pitchTargetDeg = math.clamp(pitchTargetDeg - orbit.y * k * (settings.InvertOrbitY ? -1 : 1), MinPitch, MaxPitch);
            distanceTarget = math.clamp(distanceTarget * math.pow(config.ZoomStepFactor, input?.ZoomSteps ?? 0), MinDistance, MaxDistance);
            // C toggles free look in `chase` and `orbit` (the preset switches next frame); elsewhere it recentres
            if (input?.RecenterPressed == true && !FlightCameraPreset.ToggleFreeLook())
            {
                yawOffsetDeg = preset.Yaw == FlightCameraYaw.Free ? CameraMath.HeadingYawDeg(TargetRotation) : 0;
                pitchTargetDeg = DefaultPitch; aimYawBase = CameraMath.HeadingYawDeg(TargetRotation);
            }
            if (CutRequested)
            {
                // A spawn, teleport or jump faces the ship's heading again in the presets that do not follow it.
                aimYawBase = CameraMath.HeadingYawDeg(TargetRotation);
                if (preset.Yaw == FlightCameraYaw.Free && !preset.UseConfig) yawOffsetDeg = aimYawBase;
            }
            float desiredYaw;
            switch (preset.UseConfig ? FlightCameraYaw.Config : preset.Yaw)
            {
                case FlightCameraYaw.Heading: desiredYaw = CameraMath.HeadingYawDeg(TargetRotation) + yawOffsetDeg; break;
                case FlightCameraYaw.Free: desiredYaw = yawOffsetDeg; break;
                case FlightCameraYaw.Aim:
                    // Turn while the cursor is towards a side of the screen, faster the nearer the edge; the middle is calm. Screen space,
                    // so a cursor near the ship never sets the view spinning.
                    if (hasPointer)
                    {
                        float side = pointer01.x * 2 - 1, excess = math.abs(side) - preset.AimDeadZoneDeg / 100f;
                        if (excess > 0) aimYawBase += math.sign(side) * math.min(excess * preset.AimTurnGain, 1) * preset.AimMaxTurnDegPerSecond * dt;
                    }
                    desiredYaw = aimYawBase + yawOffsetDeg; break;
                default: desiredYaw = (settings.YawMode == CameraYawMode.FollowHeading ? CameraMath.HeadingYawDeg(TargetRotation) : 0) + yawOffsetDeg; break;
            }
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
                float3 offset = FocusState - previousFollowPosition;
                UpdateFollowFocus(targetLookAhead, dt);
                // Cruise: the focus rides with the ship and only the offset it had decays. The follow filter lags by speed × 0.087 s,
                // 430 m at 5 km/s, and left the ship a dot ahead of the camera (owner, 2026-10-21).
                if (cruiseBlend > 0)
                    FocusState = math.lerp(FocusState, TargetPosition + offset * math.exp(-math.LN2 / config.FocusHalfLife * dt), cruiseBlend);
            }
            previousFollowPosition = TargetPosition;
            rotation = CameraMath.OrbitRotation(pitchDeg, yawDeg);
            float framed = distance * FramingScale, limit = math.min(framed, occlusionDistance);
            if (limit < framed) occlusionRecovering = true;
            if (!occlusionRecovering || limit <= effectiveDistance) effectiveDistance = limit;
            else effectiveDistance = CameraMath.Damp(effectiveDistance, limit, config.OcclusionReleaseHalfLife, dt);
            if (math.abs(effectiveDistance - framed) < .0001f) { effectiveDistance = framed; occlusionRecovering = false; }
            framingOffset = FrameFlightFocus(rotation, effectiveDistance) - FocusState;
            position = CameraMath.OrbitPosition(Focus, rotation, effectiveDistance);
            if (TiltUp != 0) rotation = CameraMath.OrbitRotation(pitchDeg - TiltUp, yawDeg);
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
            // QA-001: frame inside a 1 % inset so a frame of motion between solve and render never puts a corner past the edge.
            float tanY = math.tan(math.radians(camera.fieldOfView * .5f)) * .99f, tanX = tanY * camera.aspect;
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
            cruiseEffects?.Dispose(); cruiseEffects = null;
            if (instance != null)
            {
                if (Application.isPlaying) Object.Destroy(instance); else Object.DestroyImmediate(instance);
            }
            instance = null; camera = null; listenerPivot = null;
        }
    }
}
