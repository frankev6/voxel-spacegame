using Game.Voxel.Model;
using Unity.Burst;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.2 step 1: scheduled serially because functional entities may share a grid.</summary>
    [BurstCompile, WithChangeFilter(typeof(PowerLink))]
    internal partial struct PowerRebindDetectJob : IJobEntity
    {
        public ComponentLookup<ShipPower> Powers;
        void Execute(in FunctionalBlock functional, in PowerLink link)
        {
            if (link.Bound != 0 || !Powers.HasComponent(functional.Grid)) return;
            var power = Powers[functional.Grid]; power.Flags &= ~ShipPowerFlags.Initialized; Powers[functional.Grid] = power;
        }
    }
}
