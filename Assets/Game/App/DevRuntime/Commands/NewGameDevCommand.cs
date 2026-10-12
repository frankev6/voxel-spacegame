#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Core;
using Game.Core.Flow;
using Game.Persistence.Services;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>DEV-008: choose an isolated profile, then use the same new-game path as the UI.</summary>
    public sealed class NewGameDevCommand : IDevCommand
    {
        public string Name => "newgame";
        public string Usage => "newgame seed=<ulong> [ship=<start scenario>] [intro=false] [profile=harness]";
        public string Summary => "Start a new campaign and wait for player readiness";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "seed", "ship", "intro", "profile");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 0 || !args.Has("seed")) return DevCommandResult.Fail("expected " + Usage);
            ulong seed = args.GetULong("seed", 1); bool intro = args.GetBool("intro", false);
            var ship = new ContentId(args.GetString("ship", ""));
            // SPEC-70 Q17: no StartScenario content/consumer exists yet. Never silently ignore a requested selection.
            if (!ship.IsNone) return DevCommandResult.Fail("argument 'ship' selects a start scenario; no start scenarios are implemented yet (omit ship for the configured starter)");
            string profile = args.GetString("profile", "harness").Trim();
            if (profile.Length == 0 || profile.Length > 32) return DevCommandResult.Fail("argument 'profile' expects 1 to 32 characters");
            if (!ctx.TryGetService<IGameFlow>(out var flow) || !ctx.TryGetService<SaveService>(out var save)) return DevCommandResult.Fail("game flow not available");
            if (flow.IsTransitioning || save.IsBusy) return DevCommandResult.Fail("game flow or save is busy");
            if (flow.CurrentScene != GameScene.MainMenu && flow.CurrentScene != GameScene.World) return DevCommandResult.Fail("newgame requires MainMenu or World");
            SelectProfile(save, profile);
            int id = ctx.Host.AddOperation(new SessionDevOperation(flow.NewGameAsync(new NewGameArgs("Harness", seed, false, ship)), true, intro, 60));
            return DevCommandResult.Pending(id, "starting new game");
        }
        internal static void SelectProfile(SaveService save, string name)
        {
            foreach (var profile in save.ListProfiles())
                if (string.Equals(profile.DisplayName, name, StringComparison.Ordinal)) { save.SetActiveProfile(profile.Id); return; }
            save.SetActiveProfile(save.CreateProfile(name).Id);
        }
    }
}
#endif
