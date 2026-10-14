#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Core.Flow;
using Game.Core.Save;
using Game.Persistence.Services;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-70 DEV-018: manual saves use the same active-profile flow as the UI.</summary>
    public sealed class SaveDevCommand : IDevCommand
    {
        public string Name => "save";
        public string Usage => "save slot=<slot>";
        public string Summary => "Save the current campaign through game flow";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = SaveLoadDevOperation.ValidateSlot(args, Name, out string slot);
            if (error != null) return DevCommandResult.Fail(error);
            if (!ctx.TryGetService<IGameFlow>(out var flow) || !ctx.TryGetService<SaveService>(out var save))
                return DevCommandResult.Fail("game flow or save service not available");
            if (flow.IsTransitioning || save.IsBusy) return DevCommandResult.Fail("game flow or save is busy");
            if (flow.CurrentScene != GameScene.World || !flow.HasActiveSession) return DevCommandResult.Fail("save requires an active World session");
            var block = flow.GetSaveBlock();
            if (block != SaveBlockReason.None) return DevCommandResult.Fail("save blocked: " + block);
            long baseline = ctx.Events.Count("GameSaved");
            int id = ctx.Host.AddOperation(new SaveLoadDevOperation(flow.SaveGameAsync(slot, SaveReason.Manual),
                false, save.ActiveProfile.Value, slot, baseline));
            return DevCommandResult.Pending(id, "saving slot " + slot);
        }
    }
}
#endif
