using Game.Data.Combat;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.Damage
{
    public struct ImpulseRequest
    {
        public Entity Body;            // any root with PhysicsVelocity + PhysicsMass (grid, chunk body)
        public float3 WorldImpulse;    // N·s
        public float3 WorldPoint;      // local-frame world point of application
        public ProducerKind Producer;
        public uint Sequence;          // unique per (Producer, producer's own key) within a tick
        public ImpulseRequestFlags Flags;
    }
}
