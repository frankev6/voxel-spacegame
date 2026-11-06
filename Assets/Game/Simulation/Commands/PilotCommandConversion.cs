using Game.Core.Components;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>ADR-0117: the one conversion of a player's sampled command into its ship's ShipCommand (SPEC-20 §5.1), for the local
    /// player (PlayerCommandApplySystem) and co-op guests on the host (RemotePilotApplySystem): clamping, and one pending press of each
    /// edge consumed per fixed tick.</summary>
    public static class PilotCommandConversion
    {
        public static ShipCommand Convert(in PlayerCommand player, ref PlayerEdgeCounters consumed)
        {
            var edges = Consume(player.Edges.Interact, ref consumed.Interact, ShipCommandEdges.Interact);
            edges |= Consume(player.Edges.Ability1, ref consumed.Ability1, ShipCommandEdges.Ability1);
            edges |= Consume(player.Edges.Ability2, ref consumed.Ability2, ShipCommandEdges.Ability2);
            edges |= Consume(player.Edges.Ability3, ref consumed.Ability3, ShipCommandEdges.Ability3);
            edges |= Consume(player.Edges.Ability4, ref consumed.Ability4, ShipCommandEdges.Ability4);
            edges |= Consume(player.Edges.TargetNext, ref consumed.TargetNext, ShipCommandEdges.TargetNext);
            edges |= Consume(player.Edges.TargetUnderCursor, ref consumed.TargetUnderCursor, ShipCommandEdges.TargetUnderCursor);
            edges |= Consume(player.Edges.TargetClear, ref consumed.TargetClear, ShipCommandEdges.TargetClear);
            edges |= Consume(player.Edges.AutofireToggle, ref consumed.AutofireToggle, ShipCommandEdges.AutofireToggle);
            edges |= Consume(player.Edges.Cruise, ref consumed.Cruise, ShipCommandEdges.Cruise);   // ADR-0122
            bool3 finiteAim = math.isfinite(player.AimWorld);
            return new ShipCommand
            {
                Thrust = math.clamp(math.select(float2.zero, player.Thrust, math.isfinite(player.Thrust)), -1, 1),
                Turn = math.isfinite(player.Turn) ? math.clamp(player.Turn, -1, 1) : 0,
                Lift = math.isfinite(player.Lift) ? math.clamp(player.Lift, -1, 1) : 0,
                AimWorld = math.select(float3.zero, player.AimWorld, finiteAim),
                AimValid = math.all(finiteAim) ? player.AimValid : (byte)0,
                Flags = player.Flags, Edges = edges, FireGroups = player.FireGroups
            };
        }

        /// <summary>SPEC-92 §4.3: a held pilot brakes in place and cannot fire; Interact still passes (pilots ready up with it).</summary>
        public static ShipCommand Hold(ShipCommand command)
        {
            command.Thrust = float2.zero; command.Turn = 0; command.Lift = 0; command.Flags = ShipCommandFlags.Brake; command.FireGroups = 0;
            command.Edges &= ShipCommandEdges.Interact;
            return command;
        }

        static ShipCommandEdges Consume(byte incoming, ref byte consumed, ShipCommandEdges edge)
        {
            if (incoming == consumed) return ShipCommandEdges.None;
            consumed = unchecked((byte)(consumed + 1));
            return edge;
        }
    }
}
