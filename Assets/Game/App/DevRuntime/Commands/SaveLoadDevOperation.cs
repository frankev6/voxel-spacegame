#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.App.DevRuntime.Scenarios;
using Game.Core.Flow;
using UnityEngine;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>Wait for one flow result and the matching real save/load event, without retaining subscriptions.</summary>
    internal sealed class SaveLoadDevOperation : IDevOperation
    {
        readonly Awaitable<FlowResult> flow;
        readonly bool load;
        readonly string profile, slot;
        readonly long baseline;
        bool flowDone;
        public DevOperationStatus Status { get; private set; }
        public string Message { get; private set; }
        public double TimeoutSeconds => 30;
        internal SaveLoadDevOperation(Awaitable<FlowResult> flow, bool load, string profile, string slot, long baseline)
        {
            this.flow = flow; this.load = load; this.profile = profile; this.slot = slot; this.baseline = baseline;
            Message = (load ? "loading " : "saving ") + slot;
        }
        public void Tick(DevContext ctx)
        {
            if (Status != DevOperationStatus.Running) return;
            if (!flowDone)
            {
                var awaiter = flow.GetAwaiter(); if (!awaiter.IsCompleted) return;
                flowDone = true; var result = awaiter.GetResult();
                if (result != FlowResult.Ok)
                { Status = DevOperationStatus.Failed; Message = (load ? "load" : "save") + " returned " + result; return; }
            }
            bool observed = load
                ? ctx.Events.Count("GameLoaded") > baseline && ctx.Events.LastGameLoaded.ProfileId == profile && ctx.Events.LastGameLoaded.SlotId == slot
                : ctx.Events.Count("GameSaved") > baseline && ctx.Events.LastGameSaved.ProfileId == profile && ctx.Events.LastGameSaved.SlotId == slot;
            if (!observed || load && !PlayerScenarioProbes.IsReady(ctx)) return;
            // The canonical PLOC participant and player spawner own position restoration.
            Status = DevOperationStatus.Succeeded; Message = (load ? "loaded " : "saved ") + profile + "/" + slot;
        }
        internal static string ValidateSlot(DevArgs args, string command, out string slot)
        {
            slot = null; string error = DevCommandRegistry.UnknownArgument(args, command, "slot");
            if (error != null) return error;
            if (args.Positional.Count != 0 || !args.TryGetString("slot", out slot) || string.IsNullOrEmpty(slot))
                return "expected " + command + " slot=<slot>";
            // SPEC-05: slot folder names are ASCII [a-z0-9_], never paths or display names.
            foreach (char c in slot) if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_'))
                return "argument 'slot' expects lowercase ASCII letters, digits or underscores";
            return null;
        }
    }
}
#endif
