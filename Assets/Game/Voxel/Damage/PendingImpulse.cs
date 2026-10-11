using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>Knockback accumulated by the resolver at Tick; applied by ImpactEventSystem at Tick+1. Writer: DamageResolveSystem.</summary>
    public struct PendingImpulse : IComponentData
    {
        public float3 LinearWorld;     // Σ J
        public float3 AngularWorld;    // Σ (p − com) × J, world space
        public uint Tick;
    }
}
