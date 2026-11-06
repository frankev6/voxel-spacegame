using Game.Data.Components;
using Game.Simulation.Commands;
using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Simulation.Flight
{
    /// <summary>Root composition of flying grids (ADR-0015). Registered once; GridSpawnSystem adds the types at conversion. Implements SPEC-20 §4.5.</summary>
    public static class FlightSetup
    {
        public static ComponentTypeSet RootTypes => new ComponentTypeSet(new Unity.Collections.FixedList128Bytes<ComponentType>
        {
            ComponentType.ReadWrite<ShipCommand>(), ComponentType.ReadWrite<FlightState>(),
            ComponentType.ReadWrite<FlightThruster>(), ComponentType.ReadWrite<PropulsionStats>(),
            ComponentType.ReadWrite<PlanarConstraint>(), ComponentType.ReadWrite<Cruise>()   // ADR-0122
        });

        /// <summary>Appends flying profile types and the player-only tag, once from ThrusterInitSystem.OnCreate.</summary>
        public static void Register(DynamicBuffer<RootComponentRegistration> registrations)
        {
            var types = RootTypes;
            for (GridProfile profile = GridProfile.PlayerShip; profile <= GridProfile.Dummy; profile++)
            {
                if (profile == GridProfile.Station) continue;
                for (int i = 0; i < types.Length; i++)
                    registrations.Add(new RootComponentRegistration { Profile = profile, Type = types.GetComponentType(i) });
            }
            registrations.Add(new RootComponentRegistration { Profile = GridProfile.PlayerShip, Type = ComponentType.ReadWrite<PlayerShip>() });
            // ADR-0123: experimental 3D flight is a player-ship mode. Flight3D is added enabled; Flight3DModeSystem disables it on the
            // first tick because the request is off.
            registrations.Add(new RootComponentRegistration { Profile = GridProfile.PlayerShip, Type = ComponentType.ReadWrite<Flight3D>() });
            registrations.Add(new RootComponentRegistration { Profile = GridProfile.PlayerShip, Type = ComponentType.ReadWrite<Flight3DRequest>() });
        }
    }
}
