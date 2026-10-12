using Unity.Mathematics;

namespace Game.Presentation.Vfx
{
    /// <summary>Implements SPEC-63 §4.3; layout is shared with the effect shaders.</summary>
    public struct TracerInstance { public float3 Head; public float Length; public float3 Dir; public float Width; public float4 Color; }
}
