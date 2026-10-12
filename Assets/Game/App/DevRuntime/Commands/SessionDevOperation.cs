#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.App.DevRuntime.Scenarios;
using Game.App.Session;
using Game.Core;
using Game.Core.Components;
using Game.Core.Flow;
using Game.Input;
using Unity.Entities;
using UnityEngine;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>Wait for the flow once, then for the concrete playable condition; host supplies the real-time timeout.</summary>
    internal sealed class SessionDevOperation : IDevOperation
    {
        readonly Awaitable<FlowResult> flow;
        readonly bool player, intro;
        bool flowDone;
        public DevOperationStatus Status { get; private set; }
        public string Message { get; private set; } = "entering world";
        public double TimeoutSeconds { get; }
        internal SessionDevOperation(Awaitable<FlowResult> flow, bool player, bool intro, double timeout)
        { this.flow = flow; this.player = player; this.intro = intro; TimeoutSeconds = timeout; }
        public void Tick(DevContext ctx)
        {
            if (Status != DevOperationStatus.Running) return;
            if (!flowDone)
            {
                var awaiter = flow.GetAwaiter(); if (!awaiter.IsCompleted) return;
                flowDone = true; var result = awaiter.GetResult();
                if (result != FlowResult.Ok) { Status = DevOperationStatus.Failed; Message = "flow returned " + result; return; }
            }
            bool dialog = intro && ctx.TryGetService<InputService>(out var input) && input.CurrentContext == InputContext.Dialog;
            if (!dialog && (player ? !PlayerScenarioProbes.IsReady(ctx) : !ctx.HasGameplay)) return;
            var root = ctx.PlayerShipRoot;
            ulong id = root != Entity.Null && ctx.EntityManager.HasComponent<PersistentId>(root)
                ? ctx.EntityManager.GetComponentData<PersistentId>(root).Value : 0;
            ctx.TryGetService<GameSession>(out var session);
            Message = "in world: seed " + (session?.CampaignSeed ?? 0) + ", player id " + id + (dialog ? ", intro dialog" : "");
            Status = DevOperationStatus.Succeeded;
        }
    }
}
#endif
