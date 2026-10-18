using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Mechanisms
{
    /// <summary>A moving structure (ADR-0119 decision 1, SPEC-91 §4.6). On kinematic roots only; MoverSystem is the writer of
    /// their PhysicsVelocity, tick-exact from StartTick. Origin is local-frame and shifted by OriginRebaseSystem.</summary>
    public struct Mover : IComponentData
    {
        public MoverPath Path;
        public float3 Origin;
        public float Rate;          // Rotate: rad/s; Toggle: 1 / travel seconds
        public float Speed;         // Rail, Drift: m/s
        public uint StartTick;
        public byte Playing, Reverse;
        public ushort Group;        // mechanism group (actions address movers by group)
    }

    /// <summary>Rail and Toggle points, local frame (shifted by OriginRebaseSystem).</summary>
    [InternalBufferCapacity(0)] public struct MoverPoint : IBufferElementData { public float3 Pose; public float Yaw; }
}
