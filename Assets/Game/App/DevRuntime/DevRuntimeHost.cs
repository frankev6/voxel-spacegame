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
        int lastId;
        bool disposed;
        public DevCommandRegistry Commands { get; private set; }
        public DevContext Context { get; private set; }
        public DevLogCapture Logs { get; private set; }
        public DevInputInjector Input { get; private set; }
        public DevUiQuery Ui { get; private set; }
        public DevEventCounter Events { get; private set; }
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
                host.Commands = DevCommandRegistry.CreateDefault();
                return host;
            }
            catch { host.Dispose(); throw; }
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
            // DEV-006 advances scenario runners after operations.
        }
        void LateUpdate() { /* DEV-012 adds overlay rendering. */ }
        void OnDestroy() { Cleanup(); }
        void Cleanup()
        {
            if (disposed) return;
            disposed = true;
            foreach (var op in operations.Values) if (op.Status == DevOperationStatus.Running) op.Fail("play mode exited");
            running.Clear();
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
