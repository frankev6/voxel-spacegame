using System;
using Unity.Entities;

namespace Game.Simulation
{
    /// <summary>Implements SPEC-21 weapon contract: CombatGridSetup.</summary>
    public static class CombatGridSetup
    {
        public static ComponentTypeSet RootTypes => throw new NotImplementedException();   // WeaponControl, ShipTarget, WeaponStats (zero)
        /// Appends RootTypes for every combat profile (PlayerShip, NpcShip, Station, SmallCraft, Dummy). Called by WeaponInitSystem.OnCreate.
        public static void Register(DynamicBuffer<Game.Voxel.Model.RootComponentRegistration> registrations) => throw new NotImplementedException();
    }
}
