using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.6. Powered grid root composition (ADR-0015), added zeroed by GridSpawnSystem.</summary>
    public static class PowerGridSetup
    {
        /// <summary>ShipPower, PowerConsumer, ShipStats, ShipStatMultipliers, PowerStats, ShieldStats, ShipStatModifier,
        /// ShieldState, ShieldRipple and ShieldControl, all zero/empty. PowerInitJob sets the first-init values.</summary>
        public static ComponentTypeSet RootTypes
        {
            get
            {
                var types = new FixedList128Bytes<ComponentType>
                {
                    ComponentType.ReadWrite<ShipPower>(), ComponentType.ReadWrite<PowerConsumer>(),
                    ComponentType.ReadWrite<ShipStats>(), ComponentType.ReadWrite<ShipStatMultipliers>(),
                    ComponentType.ReadWrite<PowerStats>(), ComponentType.ReadWrite<ShieldStats>(),
                    ComponentType.ReadWrite<ShipStatModifier>(), ComponentType.ReadWrite<ShieldState>(),
                    ComponentType.ReadWrite<ShieldRipple>(), ComponentType.ReadWrite<ShieldControl>(),
                };
                return new ComponentTypeSet(types);
            }
        }

        /// <summary>Appends RootTypes for PlayerShip, NpcShip, Station, SmallCraft and Dummy; called by PowerRegistrationSystem.</summary>
        public static void Register(DynamicBuffer<RootComponentRegistration> registrations)
        {
            var types = RootTypes;
            for (GridProfile profile = GridProfile.PlayerShip; profile <= GridProfile.Dummy; profile++)
                for (int i = 0; i < types.Length; i++)
                    registrations.Add(new RootComponentRegistration { Profile = profile, Type = types.GetComponentType(i) });
        }
    }
}
