using Game.Simulation.Mechanisms;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Extraction
{
    public enum HeistPhase : byte { Calm = 0, Alarm = 1, Lockdown = 2, Over = 3 }
    public enum HeistOutcome : byte { None = 0, Escaped = 1, Caught = 2, Installed = 3 }
    public enum AlarmCause : byte { None = 0, CoreFreed = 1, Identified = 2, Forced = 3 }

    /// <summary>SPEC-93 decision 8: one heist place's state, on its own entity with its AlarmStep buffer. Writer: HeistDirector
    /// (authority); the place setup creates it. The alarm starts when the place's core is freed, or earlier when a crew ship is Wanted
    /// by the place's faction (they know someone is here).</summary>
    public struct HeistState : IComponentData
    {
        public HeistPhase Phase;
        public HeistOutcome Outcome;
        public AlarmCause Cause;
        public byte Faction;             // the place's faction (its watchers and garrison)
        public Entity Core;              // the place's StationCore
        public uint AlarmTick, OverTick;
        public float EscapeRadius;       // m from the core's hold point: past it with the core, the crew escaped
        public byte CruiserArrived;
    }

    public enum AlarmStepKind : byte { Mechanism = 0, Wave = 1, Message = 2, Cruiser = 3 }

    /// <summary>One beat of the alarm: AtSeconds after the alarm starts, once. Mechanism steps go to MechanismRequests (doors, zones,
    /// portals, turrets); Wave, Message and Cruiser steps go to HeistSignals for the place's runtime (spawns, toasts).</summary>
    [InternalBufferCapacity(8)]
    public struct AlarmStep : IBufferElementData
    {
        public float AtSeconds;
        public AlarmStepKind Kind;
        public MechanismActionKind Action;
        public ushort Group;
        public float Value;
        public byte Done;
    }

    public struct HeistSignal
    {
        public Entity Place;
        public AlarmStepKind Kind;
        public ushort Group;
        public float Value;
        public byte Index;               // the step's index in its buffer (its text and wave are the place's data)
        public HeistPhase Phase;         // for phase changes (Kind = Message, Index = 255)
        public HeistOutcome Outcome;
    }

    /// <summary>Singleton: heist beats for the place runtime (App, authority). HeistDirector appends; the runtime drains it.</summary>
    public struct HeistSignals : IComponentData { public NativeList<HeistSignal> Pending; }
}
