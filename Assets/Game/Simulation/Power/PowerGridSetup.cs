using System;
using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.6. Powered grid root composition (ADR-0015), added zeroed by GridSpawnSystem.</summary>
    public static class PowerGridSetup
    {
        /// <summary>ShipPower, PowerConsumer, ShipStats, ShipStatMultipliers, PowerStats, ShieldStats, ShipStatModifier,
        /// ShieldState, ShieldRipple and ShieldControl, all zero/empty. PowerInitJob sets the first-init values.</summary>
        public static ComponentTypeSet RootTypes => throw new NotImplementedException("PWR-003");

        /// <summary>Appends RootTypes for PlayerShip, NpcShip, Station, SmallCraft and Dummy; called by PowerRegistrationSystem.</summary>
        public static void Register(DynamicBuffer<RootComponentRegistration> registrations)
            => throw new NotImplementedException("PWR-003");
    }
}
