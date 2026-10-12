using Game.Presentation.Data;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Presentation.Components
{
    /// <summary>Unmanaged camera view written by CameraTargetSyncSystem (SPEC-62 §4.1).</summary>
    public struct MainCameraState : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
        public float VerticalFovDeg;
        public float Aspect;
        public float NearClip, FarClip;
        public float2 ScreenSizePx;
        public float4x4 ViewProjection; // projectionMatrix × worldToCameraMatrix; no GPU projection adjustment
        public CameraMode Mode;
        public uint FrameIndex;
    }
}
