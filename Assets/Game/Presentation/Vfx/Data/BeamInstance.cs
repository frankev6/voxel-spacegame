using Unity.Mathematics;

namespace Game.Presentation.Vfx
{
    /// <summary>Implements SPEC-63 §4.3; layout is shared with the effect shaders.</summary>
    public struct BeamInstance   { public float3 Start; public float Width; public float3 End; public float Intensity; public float4 Color; }
}
