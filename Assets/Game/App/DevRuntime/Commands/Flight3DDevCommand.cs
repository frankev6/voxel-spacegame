#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Data.Components;
using Game.Simulation.Flight;
using Unity.Entities;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>DEV-031 (ADR-0123, amended 2026-11-08): switches experimental 3D flight for every ship. It is on by default. Writes only
    /// the world switch Flight3DMode (SPEC-70 §5.4); Flight3DModeSystem does the rest. Solo only: co-op guests and the host would disagree
    /// about ships off the plane, so the mode counts as off in co-op.</summary>
    public sealed class Flight3DDevCommand : IDevCommand
    {
        internal const string Keys = "Y/H up and down, B face the mouse, P face the target (pad: Select + D-pad)";
        public string Name => "flight3d";
        public string Usage => "flight3d [on|off|status]";
        public string Summary => "Experimental 3D flight for every ship (on by default): up/down thrust and facing the mouse or the target";
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

        /// <summary>Sets the world switch (<paramref name="on"/> null only reports) and reports it, with the player ship's state when
        /// there is one. Main thread, between world updates.</summary>
        internal static bool TryApply(EntityManager em, Entity player, bool? on, out string text)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<Flight3DMode>());
            if (query.CalculateEntityCount() != 1) { text = "no experimental 3D flight switch in this world"; return false; }
            em.CompleteDependencyBeforeRW<Flight3DMode>();
            if (on.HasValue) query.SetSingleton(new Flight3DMode { On = on.Value ? (byte)1 : (byte)0 });
            bool requested = query.GetSingleton<Flight3DMode>().On != 0;
            bool hasShip = player != Entity.Null && em.Exists(player) && em.HasComponent<Flight3D>(player);
            em.CompleteDependencyBeforeRO<Flight3D>();
            bool active = hasShip && em.IsComponentEnabled<Flight3D>(player);
            text = requested ? (!hasShip || active ? "3D flight on for every ship: " : "3D flight starting for every ship: ") + Keys
                : active ? "3D flight off: ships level, then go back to the plane" : "3D flight off (planar flight)";
            return true;
        }
    }
}
#endif
