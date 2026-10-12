using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.3: AiBrain.</summary>
    public struct AiBrain : IComponentData
    {
        public AiState State;
        public sbyte OrbitSign;          // +1 or −1
        public byte FireBlocked;         // 1 = an ally is in the line of fire (autofire suspended)
        public byte Initialized;         // 0 = zeroed by profile conversion; AiAnchorShiftSystem completes it (§5.3 step 0)
        public byte AbilityEdges;        // bits 0..3 = Ability1..4 chosen on the last think (§5.4 step 8)
        public AiState PreviousState;    // state before the last think (EngageStart trigger)
        public ushort Reserved2;
        public uint AbilityEdgesTick;    // tick the edges belong to; steering writes them only on that tick
        public ushort ProfileIndex;
        public ushort ThinkCount;
        public float3 Anchor;            // local frame
        public float3 GoalPosition;      // local frame
        public float GoalHeadingYaw;     // radians (Planar convention)
        public float GoalSpeedFraction;  // 0..1 of the speed caps
        public uint LastThinkTick;
        public uint TargetSinceTick;
        public uint ShiftSequence;       // last OriginShiftRecord.Sequence applied
    }
}
