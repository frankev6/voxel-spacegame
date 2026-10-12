using Unity.Entities;
using UnityEngine;
namespace Game.Voxel.Rendering
{
    /// <summary>Explicit camera reference published by the camera owner for pooled rendering (ADR-0032).</summary>
    public struct VoxelRenderCamera : IComponentData { public UnityObjectRef<Camera> Value; }
}
