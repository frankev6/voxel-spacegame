using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Entities;

namespace Game.Simulation.Flight
{
    /// <summary>Clears listed thruster intensities once per hulk, after propulsion. Implements SPEC-20 §5.3.</summary>
    [BurstCompile]
    [WithAll(typeof(Hulk))]
    public partial struct ThrusterIdleJob : IJobEntity
    {
        public ComponentLookup<ThrusterState> Thrusters;
        void Execute(ref FlightState flight, in DynamicBuffer<FlightThruster> thrusters)
        {
            if ((flight.Flags & FlightFlags.HulkIdled) != 0) return;
            foreach (var thruster in thrusters)
                if (Thrusters.HasComponent(thruster.Thruster)) Thrusters[thruster.Thruster] = default;
            flight.Flags |= FlightFlags.HulkIdled;
        }
    }
}
