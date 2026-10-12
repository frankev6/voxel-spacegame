using UnityEngine;
using UnityEngine.VFX;

namespace Game.Presentation.Vfx
{
    /// <summary>Implements SPEC-63 §4.3; layout is shared with the effect shaders.</summary>
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct FxGpuEvent
    {
        public Vector3 position;     public float size;          // position relative to the family anchor (m); size (m)
        public Vector3 direction;    public float lifetimeScale; // unit; 1 = graph default lifetime (debris: lifetime/maxLifetime)
        public Vector3 velocity;     public uint seed;           // m/s; per-event random seed
        public Vector4 color;                                    // linear RGBA
        public uint particleCount;   public uint kind;  public Vector2 pad; // particles to keep for this event (<= ParticlesPerEvent); FxKind
    }
}
