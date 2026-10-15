using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>Co-op spike (SPEC-90 §5): turns each remote pilot's newest command into its ship's ShipCommand with the same clamping and
    /// one-press-per-tick edge rules as <see cref="PlayerCommandApplySystem"/>. Host only; runs on nothing in solo play.</summary>
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
            ref var c = ref consumed.Value;
            var edges = Consume(player.Edges.Interact, ref c.Interact, ShipCommandEdges.Interact);
            edges |= Consume(player.Edges.Ability1, ref c.Ability1, ShipCommandEdges.Ability1);
            edges |= Consume(player.Edges.Ability2, ref c.Ability2, ShipCommandEdges.Ability2);
            edges |= Consume(player.Edges.Ability3, ref c.Ability3, ShipCommandEdges.Ability3);
            edges |= Consume(player.Edges.Ability4, ref c.Ability4, ShipCommandEdges.Ability4);
            edges |= Consume(player.Edges.TargetNext, ref c.TargetNext, ShipCommandEdges.TargetNext);
            edges |= Consume(player.Edges.TargetUnderCursor, ref c.TargetUnderCursor, ShipCommandEdges.TargetUnderCursor);
            edges |= Consume(player.Edges.TargetClear, ref c.TargetClear, ShipCommandEdges.TargetClear);
            edges |= Consume(player.Edges.AutofireToggle, ref c.AutofireToggle, ShipCommandEdges.AutofireToggle);
            bool3 finiteAim = math.isfinite(player.AimWorld);
            return new ShipCommand
            {
                Thrust = math.clamp(math.select(float2.zero, player.Thrust, math.isfinite(player.Thrust)), -1, 1),
                Turn = math.isfinite(player.Turn) ? math.clamp(player.Turn, -1, 1) : 0,
                AimWorld = math.select(float3.zero, player.AimWorld, finiteAim),
                AimValid = math.all(finiteAim) ? player.AimValid : (byte)0,
                Flags = player.Flags, Edges = edges, FireGroups = player.FireGroups
            };
        }

        static ShipCommandEdges Consume(byte incoming, ref byte consumed, ShipCommandEdges edge)
        {
            if (incoming == consumed) return ShipCommandEdges.None;
            consumed = unchecked((byte)(consumed + 1));
            return edge;
        }
    }
}
