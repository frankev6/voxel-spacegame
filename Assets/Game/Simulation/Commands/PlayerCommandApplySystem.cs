using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Simulation.Commands
{
    /// <summary>Samples the managed player's latest command and consumes one pending press per fixed tick (SPEC-20 §5.1).</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ShipCommandGroup), OrderFirst = true)]
    public partial struct PlayerCommandApplySystem : ISystem
    {
        static readonly ProfilerMarker ApplyMarker = new ProfilerMarker(ProfilerCategory.Scripts, "PlayerCommandApplySystem.Apply");
        EntityQuery playerQuery, anyPlayerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<PlayerCommand>();
            var edgeEntity = state.EntityManager.CreateSingleton(default(PlayerEdgeConsumed));
            state.EntityManager.AddComponentData(edgeEntity, new PlayerControlLock { Flags = PlayerControlLockFlags.None });
            playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerShip>().WithAllRW<ShipCommand>().WithNone<Hulk>().Build();
            anyPlayerQuery = SystemAPI.QueryBuilder().WithAll<PlayerShip>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = ApplyMarker.Auto();
            var player = SystemAPI.GetSingleton<PlayerCommand>();
            ref var consumed = ref SystemAPI.GetSingletonRW<PlayerEdgeConsumed>().ValueRW.Value;
            if (anyPlayerQuery.IsEmptyIgnoreFilter || playerQuery.IsEmptyIgnoreFilter)
            {
                // Cruise belongs to the variable-rate travel consumer, including while no live player exists (Q13).
                byte cruise = consumed.Cruise;
                consumed = player.Edges; consumed.Cruise = cruise;
                return;
            }
            var command = PilotCommandConversion.Convert(player, ref consumed);   // shared with co-op guests' ships (ADR-0117)
            if (SystemAPI.TryGetSingleton(out PlayerControlLock controlLock))
            {
                if ((controlLock.Flags & PlayerControlLockFlags.Movement) != 0)
                {
                    command.Thrust = float2.zero;
                    command.Turn = 0;
                    command.Flags = ShipCommandFlags.Brake;
                }
                if ((controlLock.Flags & PlayerControlLockFlags.Actions) != 0)
                {
                    command.FireGroups = 0;
                    command.Edges = ShipCommandEdges.None;
                    // Drop the entire pending ship-action backlog, including multiple presses/counter wrap (SPEC-32 Q11).
                    // Cruise remains the variable-rate travel consumer's counter (SPEC-20 Q13).
                    byte cruise = consumed.Cruise;
                    consumed = player.Edges;
                    consumed.Cruise = cruise;
                }
            }
            state.Dependency = new PlayerCommandWriteJob { Command = command }.Schedule(playerQuery, state.Dependency);
        }
    }
}
