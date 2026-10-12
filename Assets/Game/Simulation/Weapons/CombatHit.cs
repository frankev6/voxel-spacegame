using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: CombatHit.</summary>
    public struct CombatHit { public HitKind Kind; public Entity Grid; public float Fraction; public float3 WorldPoint; public float3 ShieldLocalPoint; }
}
