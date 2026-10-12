using System;
using Game.Presentation.Data;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Presentation.Services
{
    /// <summary>Single-camera presentation service contract (SPEC-62 §6).</summary>
    public sealed class CameraRigService : IDisposable
    {
        public CameraRigService(CameraConfig config, Transform parent, ICameraInput input) => throw new NotImplementedException("CAM-004");
        public Camera Camera => throw new NotImplementedException("CAM-004");
        public CameraMode Mode => throw new NotImplementedException("CAM-004");
        public float3 Focus => throw new NotImplementedException("CAM-005");
        public float CurrentDistance => throw new NotImplementedException("CAM-005");
        public float YawDeg => throw new NotImplementedException("CAM-005");
        public void SetMode(CameraMode mode, bool cut) => throw new NotImplementedException("CAM-004");
        public void ApplySettings(in CameraSettings settings) => throw new NotImplementedException("CAM-004");
        public void SetTarget(float3 pos, quaternion rot, float3 vel, float radius, bool cut) => throw new NotImplementedException("CAM-005");
        public void ClearTarget() => throw new NotImplementedException("CAM-005");
        public void Warp(float3 delta) => throw new NotImplementedException("CAM-006");
        public void AddImpulse(float3 worldPos, float strength, float radius) => throw new NotImplementedException("CAM-008");
        public float3 DesiredFlightPosition() => throw new NotImplementedException("CAM-005");
        public void SetOcclusionDistance(float distance) => throw new NotImplementedException("CAM-007");
        public void EnterEditorOrbit(float3 center, float radius, bool cut) => throw new NotImplementedException("CAM-009");
        public void FocusOn(float3 center, float radius) => throw new NotImplementedException("CAM-009");
        public void SnapView(EditorView view) => throw new NotImplementedException("CAM-009");
        public void SetEditorSuppression(bool zoom, bool orbit) => throw new NotImplementedException("CAM-009");
        public Ray ScreenPointToRay(Vector2 screenPosBottomLeft) => throw new NotImplementedException("CAM-004");
        public void Tick(float dt) => throw new NotImplementedException("CAM-004");
        public void Dispose() => throw new NotImplementedException("CAM-004");
    }
}
