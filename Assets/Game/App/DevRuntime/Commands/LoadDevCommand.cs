#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Core.Flow;
using Game.Persistence.Services;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-70 DEV-018: restore through game flow and await the restored playable root.</summary>
    public sealed class LoadDevCommand : IDevCommand
    {
        public string Name => "load";
        public string Usage => "load slot=<slot>";
        public string Summary => "Load an active-profile slot through game flow";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = SaveLoadDevOperation.ValidateSlot(args, Name, out string slot);
            if (error != null) return DevCommandResult.Fail(error);
            if (!ctx.TryGetService<IGameFlow>(out var flow) || !ctx.TryGetService<SaveService>(out var save))
                return DevCommandResult.Fail("game flow or save service not available");
            if (flow.IsTransitioning || save.IsBusy) return DevCommandResult.Fail("game flow or save is busy");
            if (flow.CurrentScene != GameScene.MainMenu && flow.CurrentScene != GameScene.World)
                return DevCommandResult.Fail("load requires MainMenu or World");
            long baseline = ctx.Events.Count("GameLoaded");
            int id = ctx.Host.AddOperation(new SaveLoadDevOperation(flow.LoadGameAsync(slot),
                true, save.ActiveProfile.Value, slot, baseline));
            return DevCommandResult.Pending(id, "loading slot " + slot);
        }
    }
}
#endif
