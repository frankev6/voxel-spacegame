using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>ADR-0122: where a ship is in cruise. Off is normal flight.</summary>
    public enum CruisePhase : byte { Off = 0, Spooling = 1, On = 2 }

    /// <summary>ADR-0122: why the last press was refused, or why cruise ended. Add at the end only.</summary>
    public enum CruiseBlock : byte { None = 0, TooClose = 1, MassLocked = 2, Hit = 3, Arrived = 4, Cancelled = 5, Flight3D = 6 }   // Flight3D: ADR-0123

    /// <summary>ADR-0122: per-ship cruise state on every flying root (FlightSetup). Only writer: CruiseSystem. CruiseDriveSystem and
    /// PropulsionJob read it; the HUD reads the player's.</summary>
    public struct Cruise : IComponentData
    {
        public CruisePhase Phase;
        public CruiseBlock Block;        // the last refusal or ending; read with BlockTick
        public byte Exit;                // 1 on the tick cruise ended from On: CruiseDriveSystem brings the ship down to flight speed
        public uint PhaseTick;           // SimClock tick the phase began
        public uint BlockTick;           // SimClock tick of Block
        public float Speed;              // m/s along the nose while On
        public float Cap;                // m/s cap this tick (while Spooling or On)
        public float Clearance;          // m: gap between this ship's sphere and the nearest other proxy's; float.MaxValue when none
        public float LockDistance;       // m to the nearest hostile ship within MassLockRange; 0 = none
    }

    /// <summary>ADR-0122 tuning. Singleton created by CruiseSystem with <see cref="Default"/> when absent.</summary>
    public struct CruiseConfig : IComponentData
    {
        public float SlowRadius;         // 1000 m: cruise needs this much clearance, and ends where the cap meets flight speed
        public float Ramp;               // 0.3 /s: m/s of cap per m of clearance beyond SlowRadius; also the acceleration rate per m/s
        public float MaxSpeed;           // 8000 m/s
        public float MinAccel;           // 60 m/s²: the slowest a cruise builds speed
        public int SpoolTicks;           // 60
        public float MassLockRange;      // 5000 m
        public int DamageLockTicks;      // 300
        public float TurnFraction;       // 0.35 of the yaw cap while On
        public static CruiseConfig Default => new CruiseConfig { SlowRadius = 1000, Ramp = .3f, MaxSpeed = 8000, MinAccel = 60,
            SpoolTicks = 60, MassLockRange = 5000, DamageLockTicks = 300, TurnFraction = .35f };
    }

    /// <summary>ADR-0122: a ring (or a disc when InnerRadius is 0) in the plane round the entity's LocalTransform that caps cruise speed,
    /// such as an asteroid belt. Spawned by content; shifted by OriginRebaseSystem like any root.</summary>
    public struct CruiseZone : IComponentData
    {
        public float InnerRadius, OuterRadius;   // m
        public float MaxSpeed;                   // m/s
    }

    /// <summary>ADR-0122 §5: the pure cruise rules, shared by CruiseSystem and its tests.</summary>
    public static class CruiseMath
    {
        /// <summary>The speed cap for a clearance: flight speed at SlowRadius, rising by Ramp per metre beyond, never above MaxSpeed
        /// or the cap of a zone the ship is in.</summary>
        public static float Cap(in CruiseConfig c, float forwardCap, float clearance, float zoneCap) =>
            math.min(math.min(c.MaxSpeed, zoneCap), forwardCap + c.Ramp * math.max(0, clearance - c.SlowRadius));

        /// <summary>One tick of speed: up by max(MinAccel, Ramp × speed) per second, down to the cap at once.</summary>
        public static float Step(in CruiseConfig c, float speed, float cap, float dt) =>
            math.min(cap, speed + math.max(c.MinAccel, c.Ramp * speed) * dt);

        /// <summary>The cap of the zones a point is in, or float.MaxValue.</summary>
        public static float ZoneCap(float2 point, float2 zoneCentre, in CruiseZone zone)
        {
            float d = math.distance(point, zoneCentre);
            return d >= zone.InnerRadius && d <= zone.OuterRadius ? zone.MaxSpeed : float.MaxValue;
        }
    }
}
