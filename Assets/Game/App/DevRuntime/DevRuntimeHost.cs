#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core;
using Game.App.DevRuntime.Input;
using Game.App.DevRuntime.Ui;
using UnityEngine;

namespace Game.App.DevRuntime
{
    /// <summary>Owns command state and advances operations once per frame (SPEC-70 §5.2). No idle allocations.</summary>
    public sealed class DevRuntimeHost : MonoBehaviour, IDisposable
    {
        sealed class Operation : IDevOperation
        {
            readonly IDevOperation inner;
            readonly double started;
            string failure;
            public Operation(IDevOperation inner) { this.inner = inner; started = Time.realtimeSinceStartupAsDouble; }
            public DevOperationStatus Status => failure != null ? DevOperationStatus.Failed : inner.Status;
            public string Message => failure ?? inner.Message ?? string.Empty;
            public double TimeoutSeconds => inner.TimeoutSeconds;
            public void Fail(string reason) { failure = reason; }
            public void Tick(DevContext ctx)
            {
                try
                {
                    inner.Tick(ctx);
                    if (inner.Status == DevOperationStatus.Running && Time.realtimeSinceStartupAsDouble - started > TimeoutSeconds)
                        Fail("timeout after " + TimeoutSeconds.ToString(CultureInfo.InvariantCulture) + " s");
                }
                catch (Exception e) { Fail(e.GetType().Name + ": " + e.Message); }
            }
        }
        readonly Dictionary<int, Operation> operations = new Dictionary<int, Operation>();
        readonly List<int> running = new List<int>();
        readonly List<Scenarios.ScenarioRunner> scenarios = new List<Scenarios.ScenarioRunner>();
        int lastId;
        bool disposed;
        public DevCommandRegistry Commands { get; private set; }
        public DevContext Context { get; private set; }
        public Scenarios.ScenarioProbeRegistry Probes { get; private set; }
        public DevLogCapture Logs { get; private set; }
        public DevInputInjector Input { get; private set; }
        public DevUiQuery Ui { get; private set; }
        public DevEventCounter Events { get; private set; }
        internal Game.App.DevRuntime.Console.DevConsole Console { get; private set; }
        internal Commands.FlightReplaySession Replay { get; private set; }
        public static DevRuntimeHost Create(Transform parent)
        {
            var root = new GameObject("DevRuntimeHost");
            root.transform.SetParent(parent, false);
            var host = root.AddComponent<DevRuntimeHost>();
            try
            {
                host.Logs = root.AddComponent<DevLogCapture>();
                host.Events = new DevEventCounter();
                host.Input = new DevInputInjector();
                host.Ui = new DevUiQuery();
                host.Context = new DevContext(host);
                host.Replay = new Commands.FlightReplaySession();
                host.Commands = DevCommandRegistry.CreateDefault();
                host.Probes = Scenarios.ScenarioProbeRegistry.CreateDefault();
                return host;
            }
            catch { host.Dispose(); throw; }
        }
        internal void BindConsole(Game.Input.InputService input, Game.UI.Services.UIRoot ui)
        { Console = new Game.App.DevRuntime.Console.DevConsole(this, input, ui); }

        public Scenarios.ScenarioRunner RunScenario(Scenarios.ScenarioScript script, string path, int runId, string outputDirectory = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(DevRuntimeHost));
            var runner = new Scenarios.ScenarioRunner(script, path, runId, Context, outputDirectory);
            if (!runner.Finished) scenarios.Add(runner);
            return runner;
        }
        public int AddOperation(IDevOperation operation)
        {
            if (disposed) throw new ObjectDisposedException(nameof(DevRuntimeHost));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (double.IsNaN(operation.TimeoutSeconds) || double.IsInfinity(operation.TimeoutSeconds) || operation.TimeoutSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(operation), "timeout must be finite and non-negative");
            int id = checked(++lastId);
            operations.Add(id, new Operation(operation)); running.Add(id); return id;
        }
        public bool TryGetOperation(int id, out IDevOperation operation)
        {
            bool found = operations.TryGetValue(id, out var entry); operation = entry; return found;
        }
        public void Update()
        {
            if (disposed) return;
            Input?.Pump(Time.unscaledTimeAsDouble);
            // Capture the count so commands that append operations during Tick wait until the next frame.
            int count = running.Count;
            for (int i = 0; i < count; i++)
            {
                var op = operations[running[i]];
                if (op.Status == DevOperationStatus.Running) op.Tick(Context);
            }
            for (int i = count - 1; i >= 0; i--)
                if (operations[running[i]].Status != DevOperationStatus.Running) running.RemoveAt(i);
            int scenarioCount = scenarios.Count;
            for (int i = 0; i < scenarioCount; i++) scenarios[i].Step();
            for (int i = scenarioCount - 1; i >= 0; i--) if (scenarios[i].Finished) scenarios.RemoveAt(i);
            Console?.Update();
        }
        void LateUpdate() { /* DEV-012 adds overlay rendering. */ }
        void OnDestroy() { Cleanup(); }
        void Cleanup()
        {
            if (disposed) return;
            disposed = true;
            foreach (var op in operations.Values) if (op.Status == DevOperationStatus.Running) op.Fail("play mode exited");
            running.Clear();
            foreach (var scenario in scenarios) scenario.Abort("play mode exited");
            scenarios.Clear();
            Console?.Dispose();
            Replay?.Dispose();
            Context?.Dispose();
            Input?.Dispose();
            Events?.Dispose();
            if (Logs != null) Logs.enabled = false;
            Game.Core.Services.Unregister(this);
        }
        public void Dispose()
        {
            if (disposed) return;
            Cleanup();
            if (gameObject != null)
            {
                gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            }
        }
    }
}
#endif
