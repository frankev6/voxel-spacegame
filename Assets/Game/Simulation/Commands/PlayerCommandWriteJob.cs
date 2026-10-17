using Unity.Burst;
using Unity.Entities;

namespace Game.Simulation.Commands
{
    /// <summary>Writes only the player roots selected by PlayerCommandApplySystem (SPEC-20 §5.1).</summary>
    [BurstCompile]
    public partial struct PlayerCommandWriteJob : IJobEntity
    {
        public ShipCommand Command;
        [Unity.Collections.ReadOnly] public ComponentLookup<RaceHold> Holds;
        public void Execute(Entity entity, ref ShipCommand command)
            => command = Holds.HasComponent(entity) && Holds.IsComponentEnabled(entity) ? PilotCommandConversion.Hold(Command) : Command;
    }
}
