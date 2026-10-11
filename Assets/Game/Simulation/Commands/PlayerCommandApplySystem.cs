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
            state.EntityManager.CreateSingleton(default(PlayerEdgeConsumed));
            playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerShip>().WithAllRW<ShipCommand>().WithNone<Hulk>().Build();
            anyPlayerQuery = SystemAPI.QueryBuilder().WithAll<PlayerShip>().Build();
            // TODO(TRV-007): create the control-lock singleton here and apply its movement/action masks before scheduling.
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
            var edges = Consume(player.Edges.Interact, ref consumed.Interact, ShipCommandEdges.Interact);
            edges |= Consume(player.Edges.Ability1, ref consumed.Ability1, ShipCommandEdges.Ability1);
            edges |= Consume(player.Edges.Ability2, ref consumed.Ability2, ShipCommandEdges.Ability2);
            edges |= Consume(player.Edges.Ability3, ref consumed.Ability3, ShipCommandEdges.Ability3);
            edges |= Consume(player.Edges.Ability4, ref consumed.Ability4, ShipCommandEdges.Ability4);
            edges |= Consume(player.Edges.TargetNext, ref consumed.TargetNext, ShipCommandEdges.TargetNext);
            edges |= Consume(player.Edges.TargetUnderCursor, ref consumed.TargetUnderCursor, ShipCommandEdges.TargetUnderCursor);
            edges |= Consume(player.Edges.TargetClear, ref consumed.TargetClear, ShipCommandEdges.TargetClear);
            edges |= Consume(player.Edges.AutofireToggle, ref consumed.AutofireToggle, ShipCommandEdges.AutofireToggle);
            bool3 finiteAim = math.isfinite(player.AimWorld);
            var command = new ShipCommand
            {
                Thrust = math.clamp(math.select(float2.zero, player.Thrust, math.isfinite(player.Thrust)), -1, 1),
                Turn = math.isfinite(player.Turn) ? math.clamp(player.Turn, -1, 1) : 0,
                AimWorld = math.select(float3.zero, player.AimWorld, finiteAim),
                AimValid = math.all(finiteAim) ? player.AimValid : (byte)0,
                Flags = player.Flags, Edges = edges, FireGroups = player.FireGroups
            };
            state.Dependency = new PlayerCommandWriteJob { Command = command }.Schedule(playerQuery, state.Dependency);
        }

        static ShipCommandEdges Consume(byte incoming, ref byte consumed, ShipCommandEdges edge)
        {
            if (incoming == consumed) return ShipCommandEdges.None;
            consumed = unchecked((byte)(consumed + 1));
            return edge;
        }
    }
}
