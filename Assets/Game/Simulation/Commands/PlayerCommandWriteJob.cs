using Unity.Burst;
using Unity.Entities;

namespace Game.Simulation.Commands
{
    /// <summary>Writes only the player roots selected by PlayerCommandApplySystem (SPEC-20 §5.1).</summary>
    [BurstCompile]
    public partial struct PlayerCommandWriteJob : IJobEntity
    {
        public ShipCommand Command;
        public void Execute(ref ShipCommand command) => command = Command;
    }
}
