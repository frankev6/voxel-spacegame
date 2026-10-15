using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>Co-op spike (SPEC-90 §5): turns each remote pilot's command for this tick into its ship's ShipCommand through
    /// <see cref="PilotCommandConversion"/>, the local player's conversion. Host only; runs on nothing in solo play.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ShipCommandGroup)), UpdateAfter(typeof(PlayerCommandApplySystem))]
    public partial struct RemotePilotApplySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<RemotePilot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (input, consumed, command) in SystemAPI.Query<RefRO<RemotePilotInput>, RefRW<RemotePilotEdgeConsumed>, RefRW<ShipCommand>>()
                         .WithAll<RemotePilot>().WithNone<Hulk>())
                command.ValueRW = Apply(input.ValueRO, ref consumed.ValueRW);
        }

        /// <summary>Pure command conversion, shared with tests.</summary>
        public static ShipCommand Apply(in RemotePilotInput input, ref RemotePilotEdgeConsumed consumed)
        {
            var player = input.Command;
            if (consumed.Initialized == 0)
            {
                // Presses made before the ship existed are not replayed.
                consumed.Value = player.Edges; consumed.Initialized = 1;
            }
            return PilotCommandConversion.Convert(player, ref consumed.Value);   // the local player's conversion too (ADR-0117)
        }
    }
}
