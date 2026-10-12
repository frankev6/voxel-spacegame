using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponControl.</summary>
    public struct WeaponControl : Unity.Entities.IComponentData
    {
        public byte Autofire;            // 1 = fire at ShipTarget when in range and arc
        public WeaponAimMode AimMode;    // Autofire implies TargetLead
        public ushort Reserved;
    }
}
