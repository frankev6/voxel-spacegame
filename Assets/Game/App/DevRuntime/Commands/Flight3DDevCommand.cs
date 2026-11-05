#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Data.Components;
using Game.Simulation.Flight;
using Unity.Entities;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>DEV-031 (ADR-0123): switches experimental 3D flight for the player ship. Writes only Flight3DRequest (SPEC-70 §5.4);
    /// Flight3DModeSystem does the rest. Solo only: co-op guests and the host would disagree about a ship off the plane.</summary>
    public sealed class Flight3DDevCommand : IDevCommand
    {
        internal const string Keys = "Y/H up and down, B face the mouse, P face the target (pad: Select + D-pad)";
        public string Name => "flight3d";
        public string Usage => "flight3d [on|off|status]";
        public string Summary => "Experimental 3D flight for your ship: up/down thrust and facing the mouse or the target";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name);
            if (error != null || args.Positional.Count > 1) return DevCommandResult.Fail(error ?? Usage);
            string verb = args.Positional.Count == 0 ? "status" : args.Positional[0];
            if (verb != "on" && verb != "off" && verb != "status") return DevCommandResult.Fail(Usage);
            if (verb == "on" && ctx.TryGetService<Game.Core.Net.ICoopSession>(out var coop) && coop.Role != Game.Core.Net.SimRoleKind.Solo)
                return DevCommandResult.Fail("not in a co-op session: experimental 3D flight is solo only");
            var world = ctx.World;
            if (world == null || !world.IsCreated) return DevCommandResult.Fail("no game world: start a game first");
            bool? on = verb == "on" ? true : verb == "off" ? false : (bool?)null;
            return TryApply(world.EntityManager, ctx.PlayerShipRoot, on, out string text) ? DevCommandResult.Ok(text) : DevCommandResult.Fail(text);
        }

        /// <summary>Sets the request (<paramref name="on"/> null only reports). Main thread, between world updates.</summary>
        internal static bool TryApply(EntityManager em, Entity player, bool? on, out string text)
        {
            if (player == Entity.Null || !em.Exists(player) || !em.HasComponent<Flight3DRequest>(player) || !em.HasComponent<Flight3D>(player))
            { text = "no player ship with experimental 3D flight"; return false; }
            em.CompleteDependencyBeforeRW<Flight3DRequest>();
            if (on.HasValue) em.SetComponentData(player, new Flight3DRequest { On = on.Value ? (byte)1 : (byte)0 });
            em.CompleteDependencyBeforeRO<Flight3D>();
            bool requested = em.GetComponentData<Flight3DRequest>(player).On != 0, active = em.IsComponentEnabled<Flight3D>(player);
            text = requested ? (active ? "3D flight on: " : "3D flight starting: ") + Keys
                : active ? "3D flight off: levelling, then back to the plane" : "3D flight off (planar flight)";
            return true;
        }
    }
}
#endif
