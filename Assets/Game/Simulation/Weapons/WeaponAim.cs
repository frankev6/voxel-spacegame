using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponAim.</summary>
    public struct WeaponAim : IComponentData
    {
        public float Yaw;                // radians, offset from ForwardLocal about UpLocal, clamped to ±TraverseHalfArc
        public float DesiredYaw;         // radians (unclamped desire, for HUD/debug)
        public float3 FireDirLocal;      // grid-local unit direction a round fires along this tick (before spread)
        public byte HasAim;              // 1 if an aim point or target solution existed this tick
        public byte Aligned;             // 1 if |DesiredYaw − Yaw| ≤ FireCone (always 1 for fixed weapons with HasAim)
        public byte InRange;             // autofire: target within Range
        public byte Reserved;
    }
}
