using Unity.Entities;
using Unity.Mathematics;
using Game.Core;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: BeamState.</summary>
    public struct BeamState : IComponentData
    {
        public float3 MuzzleLocal;       // grid space muzzle this tick (after turret yaw)
        public float3 EndWorld;          // local-frame world point where the beam ends (hit point or range end)
        public float Width;              // m
        public byte Active;              // 1 while a pulse is running
        public byte HitSomething;
        public ushort TicksLeft;         // remaining ticks of the pulse
        public ContentHash Fx;           // WeaponDef.HitFx (beam look override)
    }
}
