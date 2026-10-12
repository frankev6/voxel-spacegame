#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.App.Flow;
using Game.Core.Flow;
using Game.Persistence.Services;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>DEV-008: empty World or a real player ship, through the canonical sandbox flow.</summary>
    public sealed class SandboxDevCommand : IDevCommand
    {
        public string Name => "sandbox";
        public string Usage => "sandbox [player=true] [seed=1]";
        public string Summary => "Enter the sandbox and wait for gameplay readiness";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "player", "seed");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 0) return DevCommandResult.Fail("expected " + Usage);
            bool player = args.GetBool("player", true); ulong seed = args.GetULong("seed", 1);
            if (!ctx.TryGetService<SceneFlow>(out var scenes) || !ctx.TryGetService<IGameFlow>(out var flow)
                || !ctx.TryGetService<SaveService>(out var save)) return DevCommandResult.Fail("game flow not available");
            if (flow.IsTransitioning || save.IsBusy) return DevCommandResult.Fail("game flow or save is busy");
            // EnterSandbox may save the old World. Do not switch a live player's profile underneath that capture.
            if (flow.HasActiveSession)
            {
                bool harness = false;
                foreach (var profile in save.ListProfiles())
                    if (profile.Id.Equals(save.ActiveProfile) && profile.DisplayName == "harness") harness = true;
                if (!harness) return DevCommandResult.Fail("sandbox requires the harness profile; use newgame profile=harness first");
            }
            else NewGameDevCommand.SelectProfile(save, "harness");
            int id = ctx.Host.AddOperation(new SessionDevOperation(scenes.EnterSandboxAsync(new SandboxArgs(player, seed)), player, false, 30));
            return DevCommandResult.Pending(id, "entering sandbox");
        }
    }
}
#endif
