#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-92 §6 dev commands on the playground's race (RACE-005).</summary>
    public sealed class RaceDevCommand : IDevCommand
    {
        public string Name => "race";
        public string Usage => "race open [laps=<n>] | race start | race end | race info | race respawn | race gate=<id> [back=<m>] | race blast [radius=<m>] [damage=<n>]";
        public string Summary => "Open Track 01 in the playground, start the countdown, end it, or show progress";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "laps", "gate", "back", "x", "z", "phase", "ago", "start", "racers");
            if (error != null) return DevCommandResult.Fail(error);
            var playground = ctx.Host.Playground;
            if (playground == null || playground.Race == null) return DevCommandResult.Fail("playground unavailable");
            var race = playground.Race;
            string first = args.Positional.Count == 0 ? "" : args.Positional[0];
            // Sent by the host to co-op guests (RACE-009).
            if (first == "view") return race.View(new Unity.Mathematics.double3(args.GetDouble("x", 0), 0, args.GetDouble("z", 0)), (int)args.GetLong("laps", 0));
            if (first == "sync") return race.Sync(args);
            if (first == "end" && !playground.Active) { race.End(); return DevCommandResult.Ok("race ended"); }
            if (first == "info" && !playground.Active) return DevCommandResult.Ok(race.Info());
            if (!playground.Active) return DevCommandResult.Fail("enter the playground first (playground)");
            if (args.Has("gate")) return race.ToGate((int)args.GetLong("gate", 0), (float)args.GetDouble("back", 40));
            string action = args.Positional.Count == 0 ? "info" : args.Positional[0];
            return action switch
            {
                "open" => race.Open(playground.Origin, (int)args.GetLong("laps", 0)),
                "start" => race.Start(),
                "end" => End(race),
                "respawn" => race.Respawn(),
                "info" => DevCommandResult.Ok(race.Info()),
                "blast" => race.Blast((float)args.GetDouble("radius", 3), (float)args.GetDouble("damage", 6000)),
                _ => DevCommandResult.Fail(Usage)
            };
        }
        static DevCommandResult End(Playground.DevPlaygroundRace race) { race.End(); return DevCommandResult.Ok("race ended; the track is gone"); }
    }
}
#endif
