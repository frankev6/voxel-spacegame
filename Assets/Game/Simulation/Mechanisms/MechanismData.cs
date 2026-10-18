using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Mechanisms
{
    /// <summary>What fires a mechanism rule; the values mirror MechanismDefinition.TriggerKind. Append only.</summary>
    public enum MechanismTriggerKind : byte { SwitchDamaged = 0, InstanceDestroyed = 1, ZoneEntered = 2, GateCrossed = 3, Timer = 4, RaceLap = 5, Narrative = 6 }

    /// <summary>A placed mechanism (HAZ-014, ADR-0119 decision 2): its rules and actions, baked from a MechanismDefinition by
    /// HazardSpawn.Mechanism. Group 0 in a rule or action means this group.</summary>
    public struct Mechanism : IComponentData { public ushort Group; }

    [InternalBufferCapacity(0)]
    public struct MechanismRule : IBufferElementData
    {
        public MechanismTriggerKind Kind;
        public ushort Group;            // the group whose switch or instance fires it; 0 = the mechanism's
        public float Value;             // ZoneEntered, GateCrossed: effect id; Timer: period s; RaceLap: lap
        public float Offset;            // Timer: s
        public ushort ActionStart, ActionCount;
    }

    [InternalBufferCapacity(0)]
    public struct MechanismRuleAction : IBufferElementData
    {
        public MechanismActionKind Kind;
        public ushort Group;            // 0 = the mechanism's group
        public float Value;
        public uint DelayTicks;
    }

    /// <summary>On a root that takes part in a mechanism group: its Switch blocks and block instances fire triggers of this group.</summary>
    public struct MechanismGroup : IComponentData { public ushort Group; }

    /// <summary>Singleton owned by MechanismSystem (authority). Current holds the actions due this tick; their owners (MoverSystem,
    /// ZoneSystem, ...) read them on their next update, which is the following tick, and apply what they own. Main thread only.</summary>
    public struct MechanismActions : IComponentData
    {
        public NativeList<MechanismAction> Current;
        public uint Tick;               // the tick Current was written on
    }

    /// <summary>Written by the race (RaceSystem) when one runs: the leader's lap, read by RaceLap triggers, and the leader's ship
    /// (place 1 while racing, else Entity.Null), read by TargetOverrideSystem for leader-targeting turrets (HAZ-020).</summary>
    public struct RaceLapSignal : IComponentData { public int LeaderLap; public Entity Leader; }
}
