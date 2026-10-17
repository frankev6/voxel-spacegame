using Game.Data.Hazards;
using Unity.Entities;

namespace Game.Simulation.Hazards
{
    /// <summary>A gate or bounds result from ZoneSystem this tick, for authority deciders (SPEC-91 §4.3).</summary>
    public struct ZoneContact
    {
        public Entity Body;
        public ulong Zone;          // WorldZone.PersistentId
        public ZoneEffectKind Kind;
        public ushort Id;           // the effect's Id
        public sbyte Crossed;       // Gate: +1 along the zone forward, −1 against it, 0 none
        public byte Inside;         // Bounds: 1 inside
    }
}
