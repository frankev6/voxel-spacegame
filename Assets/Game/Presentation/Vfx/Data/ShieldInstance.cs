using Unity.Mathematics;

namespace Game.Presentation.Vfx
{
    /// <summary>Implements SPEC-63 §4.3; layout is shared with the effect shaders.</summary>
    public struct ShieldInstance { public float4x4 EllipsoidToWorld; public float4 Color; public float HpFraction, Flash, Pad0, Pad1;
                                   public float4 Ripple0, Ripple1, Ripple2, Ripple3, Ripple4, Ripple5, Ripple6, Ripple7; }
}
