#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Core;
using Game.Core.Events;
using Game.Galaxy.Bodies;
using Game.Galaxy.Data;
using Game.Galaxy.Interaction;
using Game.Galaxy.Services;
using Game.Input;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>DEV-014: position legally, wait for the real surface query, then inject the bound Interact action.</summary>
    public sealed class InteractDevCommand : IDevCommand
    {
        public string Name => "interact";
        public string Usage => "interact [body=<key, display name or story tag>]";
        public string Summary => "Press Flight/Interact for 0.1 s; optionally move halfway inside a body's surface interaction range";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "body");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            if (!ctx.HasGameplay || ctx.PlayerShipRoot == Entity.Null) return DevCommandResult.Fail("no player ship");
            if (!ctx.TryGetService<InputService>(out var input) || input.CurrentContext != InputContext.Flight)
                return DevCommandResult.Fail("Flight input context required");
            var action = input.Controls.asset.FindAction("Flight/Interact", false);
            if (!Bound(action)) return DevCommandResult.Fail("action Flight/Interact has no binding");
            if (!Supported(action.controls[0])) return DevCommandResult.Fail("unsupported Flight/Interact control " + action.controls[0].path);
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            string before = DevBodyQuery.TargetName(ctx); BodyId desired = default; Entity target;
            if (args.TryGetString("body", out string requested))
            {
                if (string.IsNullOrWhiteSpace(requested)) return DevCommandResult.Fail("argument 'body' expects a key, display name or story tag");
                if (!ctx.TryGetService<GalaxyService>(out var galaxy) ||
                    !DevBodyQuery.TryFind(em, galaxy, requested, out target, out float radius, out float range))
                    return DevCommandResult.Fail("loaded body not found: '" + requested + "'");
                if (!math.isfinite(range) || range <= 0) return DevCommandResult.Fail("body has no positive interaction range: '" + requested + "'");
                desired = em.GetComponentData<StarSystemBody>(target).Id;
                var player = ctx.PlayerShipRoot; var pose = em.GetComponentData<LocalTransform>(player);
                var center = em.GetComponentData<LocalTransform>(target).Position;
                DevEcs.TeleportRoot(em, player, center - new float3(0, 0, radius + .5f * range), pose.Rotation);
            }
            else
            {
                if (!ctx.TryGetInteractionTarget(out var current) || current.Target == Entity.Null || current.InRange == 0)
                    return DevCommandResult.Fail("no interaction target in range");
                target = current.Target; desired = current.Body;
            }
            int id = ctx.Host.AddOperation(new InteractionOperation(ctx, input, action, desired, target, before));
            return DevCommandResult.Pending(id, "waiting for legal interaction range; before=" + DevJsonWriter.Quote(before));
        }
        static bool Bound(InputAction action) => action != null && action.enabled && action.controls.Count != 0;
        static bool Supported(InputControl control) => control is KeyControl || control.device is Mouse mouse &&
            (control == mouse.leftButton || control == mouse.rightButton || control == mouse.middleButton);
        sealed class InteractionOperation : IDevOperation
        {
            readonly World world;
            readonly Entity player, target;
            readonly BodyId body;
            readonly InputService input;
            readonly InputAction action;
            readonly InputControl control;
            readonly string before;
            readonly long session;
            long events;
            ulong targetGrid;
            bool pressed;
            public DevOperationStatus Status { get; private set; }
            public string Message { get; private set; } = "waiting for real interaction query";
            public double TimeoutSeconds => 10;
            internal InteractionOperation(DevContext ctx, InputService input, InputAction action, BodyId body, Entity target, string before)
            {
                world = ctx.World; player = ctx.PlayerShipRoot; this.input = input; this.action = action; control = action.controls[0];
                this.body = body; this.target = target; this.before = before; session = ctx.Events.Count(nameof(SessionReset));
            }
            public void Tick(DevContext ctx)
            {
                if (Status != DevOperationStatus.Running) return;
                if (ctx.World != world || !world.IsCreated || !ctx.HasGameplay || ctx.PlayerShipRoot != player ||
                    ctx.Events.Count(nameof(SessionReset)) != session)
                {
                    if (pressed) Release(ctx);
                    Status = DevOperationStatus.Failed; Message = "interaction world, player or session changed"; return;
                }
                var delivered = ctx.Events.LastBodyInteracted;
                if (pressed && ctx.Events.Count(nameof(BodyInteracted)) > events && (body.IsValid
                    ? delivered.StarSystemValue == body.System.Value && delivered.BodyKey == body.LocalKey
                    : delivered.GridPersistentId == targetGrid))
                {
                    Release(ctx);
                    Status = DevOperationStatus.Succeeded;
                    Message = "BodyInteracted delivered; before=" + DevJsonWriter.Quote(before) + " after=" + DevJsonWriter.Quote(DevBodyQuery.TargetName(ctx));
                    return;
                }
                if (input.CurrentContext != InputContext.Flight)
                {
                    if (pressed) Release(ctx);
                    Status = DevOperationStatus.Failed; Message = "interaction input context changed before BodyInteracted"; return;
                }
                if (pressed) return;
                if (!Bound(action) || action.controls[0] != control)
                { Status = DevOperationStatus.Failed; Message = "Flight/Interact binding changed before the press"; return; }
                if (!ctx.TryGetInteractionTarget(out var current) || current.Target == Entity.Null || current.InRange == 0 ||
                    (body.IsValid ? !current.Body.Equals(body) : current.Target != target)) return;
                targetGrid = current.GridPersistentId; events = ctx.Events.Count(nameof(BodyInteracted));
                ctx.Input.PressAction(action, .1); pressed = true; Message = "pressed Flight/Interact; awaiting BodyInteracted";
            }
            void Release(DevContext ctx)
            {
                if (control is KeyControl key) ctx.Input.SetKeys(key.keyCode.ToString(), 0, true);
                else if (control.device is Mouse mouse) ctx.Input.SetMouse(-1, -1,
                    control == mouse.leftButton ? "left" : control == mouse.rightButton ? "right" : "middle", false, 0, 0, 0);
            }
        }
    }
}
#endif
