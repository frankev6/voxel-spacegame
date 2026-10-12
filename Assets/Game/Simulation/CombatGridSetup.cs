using System;
using Unity.Entities;

namespace Game.Simulation
{
    /// <summary>Implements SPEC-21 weapon contract: CombatGridSetup.</summary>
    public static class CombatGridSetup
    {
        public static ComponentTypeSet RootTypes => new ComponentTypeSet(ComponentType.ReadWrite<Weapons.WeaponControl>(), ComponentType.ReadWrite<Weapons.ShipTarget>(), ComponentType.ReadWrite<Weapons.WeaponStats>());
        /// Appends RootTypes for every combat profile (PlayerShip, NpcShip, Station, SmallCraft, Dummy). Called by WeaponInitSystem.OnCreate.
        public static void Register(DynamicBuffer<Game.Voxel.Model.RootComponentRegistration> registrations)
        {
            var types = RootTypes;
            for (Game.Voxel.Model.GridProfile profile = Game.Voxel.Model.GridProfile.PlayerShip; profile <= Game.Voxel.Model.GridProfile.Dummy; profile++)
                for (int i = 0; i < types.Length; i++) registrations.Add(new Game.Voxel.Model.RootComponentRegistration { Profile = profile, Type = types.GetComponentType(i) });
        }
    }
}
