using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.4. Enabled at spawn; SPEC-12 consumes and disables.</summary>
    public struct GridBodyInit : IComponentData, IEnableableComponent
    { public GridMotion Motion; public float3 LinearVelocity; public float3 AngularVelocity; }
}
