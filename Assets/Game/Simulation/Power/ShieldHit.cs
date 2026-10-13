using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.3. Shield impact event (architecture §6.4 binding fields first). Producers: SPEC-21.</summary>
    public struct ShieldHit
    {
        public Entity Grid;             // binding: the shielded grid root
        public float3 LocalPoint;       // binding: grid space, on the ellipsoid surface (blast: blast centre in grid space)
        public float Amount;            // binding: raw damage before shield type multiplier
        public Game.Data.Combat.DamageType Type;    // binding (designed by SPEC-13, architecture §3.2)
        public Entity Source;           // binding: attacking grid root or Entity.Null
        public float3 LocalDir;         // unit direction of travel in grid space (blast: zero)
        public float Penetration;       // cells, copied to the overflow DamageEvent
        public float Radius;            // blast radius in metres (0 for point hits)
        public float3 WorldImpulse;     // copied to the overflow DamageEvent
        public ShieldHitFlags Flags;
        public Game.Data.Combat.ProducerKind Producer;   // SPEC-02 §4.12: Projectile, Beam, Explosion, Ability…
        public uint Sequence;           // producer sequence for the stable sort key
        public Entity Instigator;       // copied to the overflow DamageEvent (review X6)
    }
}
