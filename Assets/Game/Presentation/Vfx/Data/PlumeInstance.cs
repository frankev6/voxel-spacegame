using Unity.Mathematics;

namespace Game.Presentation.Vfx
{
    /// <summary>Implements SPEC-63 §4.3; layout is shared with the effect shaders.</summary>
    public struct PlumeInstance  { public float3 Nozzle; public float Radius; public float3 Dir; public float Length; public float4 Color; }
}
