using Game.Data.Combat;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>A request to damage cells of one grid, in grid-local metres. Implements SPEC-13 §4.2.</summary>
    public struct DamageEvent
    {
        public Entity Grid;            // VoxelGrid root
        public float3 LocalPoint;      // grid-local metres (§3 decision 2)
        public float3 LocalDir;        // grid-local travel direction into the grid; any length; zero allowed
        public float Amount;           // raw damage before armour multipliers; events with Amount <= 0 are dropped
        public float Radius;           // metres; 0 = point/penetrating path (§5.3.3); > 0 = blast (§5.3.4)
        public float Penetration;      // point path: max solid cells destroyed along the ray; < 1 treated as 1
        public DamageType Type;
        public Entity Source;          // attribution and sort key; Entity.Null allowed
        public float3 WorldImpulse;    // N·s at the hit point, applied next tick (§3 decision 13)
        // ---- SPEC-13 additions ----
        public ProducerKind Producer;
        public uint Sequence;          // unique per (Source, Producer) within a tick; assigned by the producer
        public DamageEventFlags Flags;
        public Entity Instigator;      // who is responsible (review X6): OwnedBy.Owner of the shooter if present, else Source. Not a sort key
    }
}
