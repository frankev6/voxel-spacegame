using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponState.</summary>
    public struct WeaponState : IComponentData
    {
        public byte Group;                   // fire-group bits (= WeaponDef.DefaultFireGroups in v1)
        public byte Firing;                  // 1 if a round or pulse was released this tick
        public ushort RoundsLeftInBurst;     // remaining rounds of the current burst
        public float CooldownRemainingTicks; // trigger accumulator (decision 7)
        public float CooldownTicks;          // = TriggerIntervalTicks / FireRateMultiplier (HUD)
        public float BurstRemainingTicks;    // burst accumulator
        public uint ShotCounter;             // rounds fired since spawn (ShotId low 16 bits)
    }
}
