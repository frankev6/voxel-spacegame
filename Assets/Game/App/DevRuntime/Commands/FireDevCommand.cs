#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Core;
using Game.Input;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-70 DEV-010: hold the real Flight fire action through state-event injection.</summary>
    public sealed class FireDevCommand : IDevCommand
    {
        readonly Func<double> now;
        public FireDevCommand() : this(() => Time.unscaledTimeAsDouble) { }
        internal FireDevCommand(Func<double> now) => this.now = now;
        public string Name => "fire";
        public string Usage => "fire [group=1..3] [seconds=0.5]";
        public string Summary => "Press a bound Flight fire action for the requested duration";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "group", "seconds");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            long group = args.GetLong("group", 1); double seconds = args.GetDouble("seconds", .5);
            if (group < 1 || group > 3) return DevCommandResult.Fail("group expects 1..3");
            if (!double.IsFinite(seconds) || seconds <= 0) return DevCommandResult.Fail("seconds expects finite positive seconds");
            if (!ctx.HasGameplay || ctx.PlayerShipRoot == Entity.Null) return DevCommandResult.Fail("no player ship");
            if (!ctx.TryGetService<InputService>(out var input) || input.CurrentContext != InputContext.Flight)
                return DevCommandResult.Fail("Flight input context required");
            string path = group == 1 ? "Flight/FirePrimary" : group == 2 ? "Flight/FireSecondary" : "Flight/FireTertiary";
            var action = input.Controls.asset.FindAction(path, false);
            if (action == null || action.controls.Count == 0) return DevCommandResult.Fail("action " + path + " has no binding");
            var control = action.controls[0];
            ctx.Input.PressAction(action, seconds);
            int operation = ctx.Host.AddOperation(new FireOperation(ctx.World, ctx.PlayerShipRoot, input, control, seconds, now));
            return DevCommandResult.Pending(operation, "holding " + path);
        }
        sealed class FireOperation : IDevOperation
        {
            readonly World world;
            readonly Entity player;
            readonly InputService input;
            readonly InputControl control;
            readonly Func<double> now;
            readonly double seconds, end;
            public FireOperation(World world, Entity player, InputService input, InputControl control, double seconds, Func<double> now)
            { this.world = world; this.player = player; this.input = input; this.control = control; this.seconds = seconds; this.now = now; end = now() + seconds; }
            public DevOperationStatus Status { get; private set; } = DevOperationStatus.Running;
            public string Message { get; private set; } = "holding fire action";
            public double TimeoutSeconds => seconds + 1;
            public void Tick(DevContext ctx)
            {
                bool changed = ctx.World != world || !world.IsCreated || !ctx.HasGameplay || ctx.PlayerShipRoot != player || input.CurrentContext != InputContext.Flight;
                if (!changed && now() < end) return;
                // Release through the same device path, including early cancellation on scene/context changes.
                if (control is KeyControl key) ctx.Input.SetKeys(key.keyCode.ToString(), 0, true);
                else if (control.device is Mouse mouse)
                {
                    string button = control == mouse.leftButton ? "left" : control == mouse.rightButton ? "right" : "middle";
                    ctx.Input.SetMouse(-1, -1, button, false, 0, 0, 0);
                }
                Status = changed ? DevOperationStatus.Failed : DevOperationStatus.Succeeded;
                Message = changed ? "fire world, player or input context changed" : "fire hold complete";
            }
        }
    }
}
#endif
