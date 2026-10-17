#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-92 §6 dev commands on the playground's race (RACE-005).</summary>
    public sealed class RaceDevCommand : IDevCommand
    {
        public string Name => "race";
        public string Usage => "race open [laps=<n>] | race start | race end | race info | race respawn | race gate=<id> [back=<m>]";
        public string Summary => "Open Track 01 in the playground, start the countdown, end it, or show progress";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "laps", "gate", "back");
            if (error != null) return DevCommandResult.Fail(error);
            var playground = ctx.Host.Playground;
            if (playground == null || playground.Race == null || !playground.Active) return DevCommandResult.Fail("enter the playground first (playground)");
            var race = playground.Race;
            if (args.Has("gate")) return race.ToGate((int)args.GetLong("gate", 0), (float)args.GetDouble("back", 40));
            string action = args.Positional.Count == 0 ? "info" : args.Positional[0];
            return action switch
            {
                "open" => race.Open(playground.Origin, (int)args.GetLong("laps", 0)),
                "start" => race.Start(),
                "end" => End(race),
                "respawn" => race.Respawn(),
                "info" => DevCommandResult.Ok(race.Info()),
                _ => DevCommandResult.Fail(Usage)
            };
        }
        static DevCommandResult End(Playground.DevPlaygroundRace race) { race.End(); return DevCommandResult.Ok("race ended; the track is gone"); }
    }
}
#endif
