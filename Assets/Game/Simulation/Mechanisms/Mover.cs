using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Mechanisms
{
    /// <summary>A moving structure (ADR-0119 decision 1, SPEC-91 §4.6). On kinematic roots only; MoverSystem is the writer of
    /// their PhysicsVelocity. Its pose is a pure function of the path and the clock from StartTick (MoverMath.Pose). Origin, the
    /// points and the pose follow origin rebases (MoverSystem).</summary>
    public struct Mover : IComponentData
    {
        public MoverPath Path;
        public float3 Origin;       // Rotate: the hub (the root's pivot); Drift: the position at StartTick (local frame)
        public float Rate;          // Rotate, Drift: yaw rad/s; Toggle: 1 / travel seconds
        public float Speed;         // Rail: m/s along the points
        public uint StartTick;
        public byte Playing, Reverse;   // Rail: Reverse runs the points backwards; Toggle: Reverse travels back to the first point
        public ushort Group;        // mechanism group (actions address movers by group)
        // HAZ-013 additions
        public float Yaw;           // Rotate, Drift: yaw at StartTick
        public float3 Velocity;     // Drift: m/s
        public byte PingPong;       // Rail: 1 = back and forth along the points, 0 = a closed loop
    }

    /// <summary>Rail and Toggle points, local frame (shifted with origin rebases).</summary>
    [InternalBufferCapacity(0)] public struct MoverPoint : IBufferElementData { public float3 Pose; public float Yaw; }
}
