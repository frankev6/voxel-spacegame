using Unity.Entities;
using Unity.Mathematics;
namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §4.3 last eight shield impacts; only ShieldSystem writes this ring.</summary>
    [InternalBufferCapacity(8)] public struct ShieldRipple : IBufferElementData
    { public float3 LocalPoint; public uint StartTick; public float Strength; }
}
