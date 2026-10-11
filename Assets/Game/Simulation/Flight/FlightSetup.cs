using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Root composition of flying grids (ADR-0015). Registered once; GridSpawnSystem adds the types at conversion. Implements SPEC-20 §4.5.</summary>
    public static class FlightSetup
    {
        /// ShipCommand, FlightState, FlightThruster buffer, PropulsionStats, Game.Data.Components.PlanarConstraint (all zero/empty).
        public static Unity.Entities.ComponentTypeSet RootTypes => throw new System.NotImplementedException("FLT-006");
        /// Appends RootTypes for GridProfile PlayerShip, NpcShip, SmallCraft, Dummy, and PlayerShip (tag) for GridProfile.PlayerShip,
        /// to the RootComponentRegistration buffer (SPEC-10 §5.13). Called by ThrusterInitSystem.OnCreate.
        public static void Register(Unity.Entities.DynamicBuffer<Game.Voxel.Model.RootComponentRegistration> registrations) => throw new System.NotImplementedException("FLT-006");
    }
}
