using Game.Data.Combat;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>Consumed by ExplosionSystem (SPEC-21). Implements SPEC-13 §4.2.</summary>
    public struct ExplosionRequest
    {
        public float3 WorldPos;        // local-frame world position
        public float Radius;           // metres
        public float Amount;           // raw damage at the centre
        public DamageType Type;
        public Entity Source;
        // ---- SPEC-13 additions ----
        public ProducerKind Producer;   // Explosion for reactor/core breaches written by DMG
        public uint Sequence;
        public Entity Instigator;       // carried into the DamageEvents/ShieldHits the explosion produces (review X6)
    }
}
