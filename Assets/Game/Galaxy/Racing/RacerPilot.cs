using Unity.Entities;
using Unity.Mathematics;

namespace Game.Galaxy.Racing
{
    /// <summary>RACE-017 (owner: "a racer ai that can go through the course and tries its best to avoid obstacles while getting to the
    /// finish line"): flies its ship round the race. On AI racers, and on a player's ship under `race autopilot`. Its ship also carries
    /// Pilot (the race slot). RacerPilotSystem writes the ship's command from the racing line and what it senses ahead; authority only.</summary>
    public struct RacerPilot : IComponentData
    {
        public float Skill;            // 0..1: corner speed and how often it takes the shorter branch
        public uint Seed;              // branch choices and the line it holds
        public short LineFrom;         // the racer's last gate when the line was built (−1 = the grid); −2 = no line yet
        public short LineTo;           // the gate the line ends at
        public byte LineLap;           // the lap it was built on
        public byte WantsReset;        // set by the steer job when stuck; the system puts the ship back at its last gate
        public int Index;              // segment of the line the ship is on
        public float Steer;            // last chosen offset from the line target (rad), for hysteresis
        public float Lane;             // −0.3..0.3: where across the free width it holds its line
        public int ProgressIndex; public uint ProgressTick;
        public uint CommitUntil;
        public uint OutsideSince;      // tick it went beyond the route's edge (0 = inside)       // after switching branch it holds to the new one until this tick   // the furthest segment reached, and when: no progress for long = stuck
        // telemetry (race ai)
        public float TargetSpeed, Threat;
        public float Wide, Off;        // the furthest it has been outside its safe band from the line, and now (m; ≤ 0 = inside)
        public float2 Aim;             // the point it steered for this tick (track space)
        public byte Replans;           // lines built (fresh, carried on or switched), for traces
    }

    /// <summary>RACE-017: the racer's line for the next few gates, relative to the track origin. Radius is the turn radius there (m).</summary>
    [InternalBufferCapacity(0)]
    public struct RacerLinePoint : IBufferElementData { public float2 Position; public float HalfWidth; public float Radius; public float PushSpeed; }
}
