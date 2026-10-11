#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Core;
using Game.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Explicit M0 probe registrations and typed reads (SPEC-70 §6.2, DEV-005).</summary>
    public static class BuiltInScenarioProbes
    {
        enum ProbeKind : byte { Scene, Frame, Tick, Paused, Context, Ready, UiVisible, Errors, Exceptions, Warnings, LogContains, Events, Ecs, Audio, Abs, Min, Max, Distance }
        sealed class Probe : IScenarioProbe
        {
            readonly ProbeKind kind;
            public string Name { get; }
            public int ArgCount { get; }
            internal Probe(string name, int args, ProbeKind kind) { Name = name; ArgCount = args; this.kind = kind; }
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args)
            {
                switch (kind)
                {
                    case ProbeKind.Scene: return Text(SceneManager.GetActiveScene().name);
                    case ProbeKind.Frame: return Number(Time.frameCount);
                    case ProbeKind.Tick: return Number(ctx.Tick);
                    case ProbeKind.Paused: return Boolean(ctx.TryGetService<IPauseService>(out var pause) && pause.IsPaused);
                    case ProbeKind.Context: return Text(ctx.TryGetService<InputService>(out var input) ? ContextName(input.CurrentContext) : "None");
                    case ProbeKind.Ready: return Boolean(ctx.ServicesReady);
                    case ProbeKind.UiVisible: return Boolean(ctx.Ui.ContainsText(RequireString(args[0])));
                    case ProbeKind.Errors: return Number(ctx.LogErrors);
                    case ProbeKind.Exceptions: return Number(ctx.LogExceptions);
                    case ProbeKind.Warnings: return Number(ctx.LogWarnings);
                    case ProbeKind.LogContains: return Boolean(ctx.Logs.ContainsSince(ctx.LogStartSequence, RequireString(args[0])));
                    case ProbeKind.Events: return Number(ctx.EventCount(RequireString(args[0])));
                    case ProbeKind.Ecs: return Number(ctx.CountEntities(RequireString(args[0])));
                    case ProbeKind.Audio: return Number(ctx.AudioListenerCount());
                    case ProbeKind.Abs: return Number(Math.Abs(RequireNumber(args[0])));
                    case ProbeKind.Min: return Number(Math.Min(RequireNumber(args[0]), RequireNumber(args[1])));
                    case ProbeKind.Max: return Number(Math.Max(RequireNumber(args[0]), RequireNumber(args[1])));
                    case ProbeKind.Distance:
                        double x = RequireNumber(args[0]) - RequireNumber(args[2]), z = RequireNumber(args[1]) - RequireNumber(args[3]);
                        return Number(Math.Sqrt(x * x + z * z));
                    default: throw new InvalidOperationException("unknown built-in probe");
                }
            }
        }
        static string ContextName(InputContext context) => context switch
        {
            InputContext.Flight => "Flight", InputContext.Editor => "Editor", InputContext.Menu => "Menu", InputContext.Dialog => "Dialog", InputContext.TextEntry => "TextEntry", _ => "None"
        };
        public static void Register(ScenarioProbeRegistry registry)
        {
            registry.Register(new Probe("scene", 0, ProbeKind.Scene)); registry.Register(new Probe("frame", 0, ProbeKind.Frame));
            registry.Register(new Probe("tick", 0, ProbeKind.Tick)); registry.Register(new Probe("paused", 0, ProbeKind.Paused));
            registry.Register(new Probe("context", 0, ProbeKind.Context)); registry.Register(new Probe("services.ready", 0, ProbeKind.Ready));
            registry.Register(new Probe("ui.visible", 1, ProbeKind.UiVisible)); registry.Register(new Probe("logs.errors", 0, ProbeKind.Errors));
            registry.Register(new Probe("logs.exceptions", 0, ProbeKind.Exceptions)); registry.Register(new Probe("logs.warnings", 0, ProbeKind.Warnings));
            registry.Register(new Probe("logs.contains", 1, ProbeKind.LogContains)); registry.Register(new Probe("events.count", 1, ProbeKind.Events));
            registry.Register(new Probe("ecs.count", 1, ProbeKind.Ecs)); registry.Register(new Probe("audio.listeners", 0, ProbeKind.Audio));
            registry.Register(new Probe("abs", 1, ProbeKind.Abs)); registry.Register(new Probe("min", 2, ProbeKind.Min));
            registry.Register(new Probe("max", 2, ProbeKind.Max)); registry.Register(new Probe("distance", 4, ProbeKind.Distance));
        }
    }
}
#endif
