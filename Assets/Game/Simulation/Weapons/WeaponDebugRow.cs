#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 WPN-015: copied current weapon values for development harness callers.</summary>
    public readonly struct WeaponDebugRow
    {
        public readonly Entity Weapon;
        public readonly ushort WeaponIndex;
        public readonly byte Group;
        public readonly float Yaw, CooldownRemainingTicks;
        public readonly byte Firing, BeamActive;

        internal WeaponDebugRow(Entity weapon, in WeaponMount mount, in WeaponState state, in WeaponAim aim, in BeamState beam)
        {
            Weapon = weapon; WeaponIndex = mount.WeaponIndex; Group = state.Group; Yaw = aim.Yaw;
            CooldownRemainingTicks = state.CooldownRemainingTicks; Firing = state.Firing; BeamActive = beam.Active;
        }
    }
}
#endif
