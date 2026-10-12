#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Core;
using Game.Input;
using Unity.Entities;
using Unity.Physics;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>DEV-008: typed existence/readiness over the real player root, collider, gameplay and input context.</summary>
    public static class PlayerScenarioProbes
    {
        internal static bool IsReady(DevContext ctx)
        {
            var root = ctx.PlayerShipRoot;
            return root != Entity.Null && ctx.HasGameplay && ctx.EntityManager.HasComponent<PhysicsCollider>(root)
                && ctx.TryGetService<InputService>(out var input) && input.CurrentContext == InputContext.Flight;
        }
        sealed class Probe : IScenarioProbe
        {
            readonly bool ready;
            public Probe(bool ready) { this.ready = ready; }
            public string Name => ready ? "player.ready" : "player.exists";
            public int ArgCount => 0;
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args)
                => Boolean(ready ? IsReady(ctx) : ctx.PlayerShipRoot != Entity.Null);
        }
        internal static void Register(ScenarioProbeRegistry registry)
        { registry.Register(new Probe(false)); registry.Register(new Probe(true)); }
    }
}
#endif
