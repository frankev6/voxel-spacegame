using Game.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Galaxy.Racing
{
    public enum RacePhase : byte { Idle = 0, Lobby = 1, Countdown = 2, Racing = 3, Finished = 4 }

    /// <summary>Singleton (SPEC-92 §4.2). Writer: RaceSystem on the authority; guests apply the replicated copy.</summary>
    public struct RaceState : IComponentData
    {
        public RacePhase Phase; public uint PhaseTick; public byte Laps; public ushort TrackGroup; public ContentHash Track;
        public uint FirstFinishTick;
        public uint StartTick;  // the GO tick (race times are measured from it)
        public float2 Origin;   // the track origin in the local frame at spawn (gate and slot positions are relative to it)
    }

    [System.Flags] public enum RacerFlags : byte { None = 0, WrongWay = 1, OutOfBounds = 2, Respawning = 4, Finished = 8, Started = 16 }

    /// <summary>One per pilot slot in the race, on the RaceState entity (SPEC-92 §4.2).</summary>
    [InternalBufferCapacity(4)]
    public struct RacerProgress : IBufferElementData
    {
        public byte Slot; public byte Lap; public short LastGate; public short ExpectedGate; public ushort BranchMask;
        public uint LapStartTick, BestLapTicks, FinishTick, LastLapTicks; public byte Place; public RacerFlags Flags;
        public uint OutOfBoundsSinceTick; public uint RespawnAtTick;
        public byte GatesThisLap;
        public Entity Ship;            // the pilot's current root
    }

    /// <summary>Authority → App session: put this pilot back on the track (SPEC-92 §5.1 steps 5–7).</summary>
    public struct RaceRespawnRequest : IComponentData { public byte Slot; public float2 Position; public float Yaw; public Entity Ship; }
}
