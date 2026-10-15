using Game.Core.Components;
using Game.Core.Systems;
using Unity.Burst;
using Unity.Entities;

namespace Game.Simulation.Commands
{
    /// <summary>ADR-0117: counts each pilot's Interact presses from this tick's ShipCommand edges, after both command-apply systems wrote
    /// them. Every machine; the authority's InteractionTriggerSystem acts on the counts.</summary>
    [BurstCompile, UpdateInGroup(typeof(ShipCommandGroup)), UpdateAfter(typeof(RemotePilotApplySystem))]
    public partial struct PilotInteractCountSystem : ISystem
    {
        [BurstCompile] public void OnCreate(ref SystemState state) => state.RequireForUpdate<GameplayActive>();
        [BurstCompile] public void OnUpdate(ref SystemState state) => state.Dependency = new CountJob().Schedule(state.Dependency);

        [BurstCompile, WithAll(typeof(Pilot))]
        partial struct CountJob : IJobEntity
        {
            void Execute(in ShipCommand command, ref PilotInteractCount count)
            {
                if ((command.Edges & ShipCommandEdges.Interact) != 0) count.Value = unchecked((byte)(count.Value + 1));
            }
        }
    }
}
