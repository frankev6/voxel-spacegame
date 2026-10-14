#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-70 DEV-018: request travel from its canonical owner; never synthesize arrival.</summary>
    public sealed class JumpDevCommand : IDevCommand
    {
        public string Name => "jump";
        public string Usage => "jump to=<StarSystemId or authored id>";
        public string Summary => "Request a dev-source lane jump through TravelService";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "to");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 0 || !args.TryGetString("to", out string value) || string.IsNullOrWhiteSpace(value))
                return DevCommandResult.Fail("expected " + Usage);
            if (!ctx.TryGetService<IGameFlow>(out var flow) || flow.CurrentScene != GameScene.World || !flow.HasActiveSession)
                return DevCommandResult.Fail("jump requires an active World session");
            if (flow.IsTransitioning) return DevCommandResult.Fail("game flow is busy");
            if (!ctx.TryGetService<TravelService>(out var travel)) return DevCommandResult.Fail("travel service not available");
            StarSystemId target;
            if (uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint number)) target = new StarSystemId(number);
            else
            {
                if (!ctx.TryGetService<GalaxyService>(out var galaxy)) return DevCommandResult.Fail("galaxy service not available");
                if (!galaxy.TryResolveStarSystem(new ContentId(value), out target))
                {
                    // IDs take precedence; display names must identify one authored system.
                    if (galaxy.Galaxy != null)
                        foreach (var system in galaxy.Galaxy.StarSystems)
                            if (system.AuthoredIndex != 0 && string.Equals(galaxy.GetStarSystemName(system.Id), value, StringComparison.OrdinalIgnoreCase))
                            {
                                if (target.IsValid) return DevCommandResult.Fail("ambiguous star system '" + value + "'; use its authored id");
                                target = system.Id;
                            }
                    if (!target.IsValid) return DevCommandResult.Fail("unknown star system '" + value + "'");
                }
            }
            if (!target.IsValid) return DevCommandResult.Fail("argument 'to' expects a nonzero StarSystemId or authored id");
            long baseline = ctx.Events.Count("StarSystemEntered");
            var result = travel.RequestJump(target, JumpSource.Dev);
            if (!result.Accepted) return DevCommandResult.Fail(result.Reason);
            int id = ctx.Host.AddOperation(new JumpOperation(travel, target, baseline, ctx.Events.Count("SessionReset")));
            return DevCommandResult.Pending(id, "jumping to " + target.Value);
        }
        sealed class JumpOperation : IDevOperation
        {
            readonly StarSystemId target;
            readonly TravelService travel;
            readonly long baseline, sessionBaseline;
            public DevOperationStatus Status { get; private set; }
            public string Message { get; private set; } = "waiting for star-system entry";
            public double TimeoutSeconds => 30;
            internal JumpOperation(TravelService travel, StarSystemId target, long baseline, long sessionBaseline)
            { this.travel = travel; this.target = target; this.baseline = baseline; this.sessionBaseline = sessionBaseline; }
            public void Tick(DevContext ctx)
            {
                if (Status != DevOperationStatus.Running) return;
                if (ctx.Events.Count("SessionReset") != sessionBaseline || !ctx.TryGetService<IGameFlow>(out var flow) ||
                    !flow.HasActiveSession || flow.CurrentScene != GameScene.World || flow.IsTransitioning ||
                    !ctx.TryGetService<TravelService>(out var current) || !ReferenceEquals(current, travel))
                { Status = DevOperationStatus.Failed; Message = "world session changed during jump"; return; }
                if (ctx.Events.Count("StarSystemEntered") > baseline &&
                    ctx.Events.LastStarSystemEntered.StarSystemValue == target.Value &&
                    ctx.Events.LastStarSystemEntered.Reason == StarSystemEntryReason.Jump)
                { Status = DevOperationStatus.Succeeded; Message = "entered star system " + target.Value; return; }
                if (travel.Phase == TravelPhase.Idle)
                { Status = DevOperationStatus.Failed; Message = "jump ended before star-system entry"; }
            }
        }
    }
}
#endif
