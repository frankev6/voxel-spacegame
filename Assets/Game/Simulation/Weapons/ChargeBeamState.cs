using Game.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §4.6d (WPN-019): charge beam phase.</summary>
    public enum ChargeBeamPhase : byte { Idle = 0, Charging = 1, Firing = 2 }

    /// <summary>SPEC-21 §4.6d (WPN-019): charge beam state on every weapon entity (Phase stays Idle for other deliveries).
    /// Only writers: ChargeBeamShotHandlerSystem (start) and ChargeBeamStepSystem (everything after). Read by SPEC-63 to draw
    /// the converging beams, the lock ring on the target and the main beam.</summary>
    public struct ChargeBeamState : IComponentData
    {
        public float3 DirWorld;          // unit: where the beam points; turns toward the aim at ≤ ChargeBeamDef.TrackRateRad
        public float3 MuzzleLocal;       // grid space muzzle this tick (after turret yaw)
        public float3 EndWorld;          // local-frame world point where the beam ends (first hit or range end)
        public Entity Target;            // grid or station the beam is on (hull or shield first hit); Entity.Null when none
        public float Width;              // m: the main beam (WeaponDef.BeamWidth)
        public ushort TicksLeft;         // remaining ticks of the phase
        public ushort PhaseTicks;        // length of the current phase in ticks (progress = 1 − TicksLeft / PhaseTicks)
        public ChargeBeamPhase Phase;
        public byte HitSomething;        // 1 if EndWorld is a hit (hull, shield or body)
        public ushort Weapon;            // WeaponDef index this sequence was started with
        public ContentHash Fx;           // WeaponDef.HitFx
    }
}
