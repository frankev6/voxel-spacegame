using Unity.Entities;
using Unity.Mathematics;
namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §4.3 analytic shield. Only ShieldSystem writes live state; VFX reads it (ADR-0060).</summary>
    public struct ShieldState : IComponentData
    {
        public float Hp, MaxHp, RegenPerSecond;
        public uint RegenResumeTick;
        public float3 Center, Radii;
        public float BoundingRadius;
        public uint ShapeRevision;
        public byte Active, RippleHead;
        public ShieldFlags Flags;
    }
}
