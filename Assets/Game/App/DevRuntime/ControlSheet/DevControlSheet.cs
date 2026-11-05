#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.App.DevRuntime.Scenarios;
using Game.Input;

namespace Game.App.DevRuntime.ControlSheet
{
    /// <summary>The control sheet (SPEC-70 DEV-029): every control and its live state on a localhost page, for a second monitor.
    /// Main thread. Closed, it does nothing; open, it rebuilds the state at most 15 times a second, and only while the page polls.</summary>
    internal sealed class DevControlSheet : IDisposable
    {
        internal const int DefaultPort = 47810;
        const double BuildInterval = 1.0 / 15, PollWindow = 2.0;
        const int MaxProbes = 64;
        readonly InputService input;
        readonly ScenarioProbeRegistry probes;
        readonly DevContext context;
        readonly string pageRoot;
        readonly List<string> probeNames = new List<string>();
        readonly Func<string, string> bindingLabel, probeJson;
        readonly DevFlightConfig flight = new DevFlightConfig();
        ControlSheetServer server;
        string parsedProbeQuery = string.Empty, flightError;
        int layoutVersion;
        bool layoutDirty;
        double nextBuild;

        /// <param name="pageRoot">The folder holding index.html; null when it was not found.</param>
        internal DevControlSheet(InputService input, ScenarioProbeRegistry probes, DevContext context, string pageRoot)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.probes = probes; this.context = context; this.pageRoot = pageRoot;
            bindingLabel = input.GetBindingDisplay;
            probeJson = ReadProbe;
        }

        internal bool IsOpen => server != null;
        internal string Url => server == null ? null : "http://127.0.0.1:" + server.Port + "/";

        /// <summary>Starts the server on the first free port of <paramref name="port"/> … <paramref name="port"/> + attempts − 1. Open already: true.</summary>
        internal bool TryOpen(int port, int attempts, out string error)
        {
            error = null;
            if (server != null) return true;
            if (pageRoot == null || !File.Exists(Path.Combine(pageRoot, "index.html")))
            { error = "page files not found: expected tools/controlsheet/index.html in the project"; return false; }
            for (int i = 0; i < attempts && server == null; i++)
            {
                var candidate = new ControlSheetServer(pageRoot);
                if (candidate.TryStart(port + i, out error)) server = candidate; else candidate.Dispose();
            }
            if (server == null) return false;
            error = null;
            input.BindingsChanged += OnBindingsChanged;
            layoutDirty = true;
            Publish();                                   // the first request already has a page to draw
            return true;
        }

        internal void Close()
        {
            if (server == null) return;
            input.BindingsChanged -= OnBindingsChanged;
            server.Dispose(); server = null;
            nextBuild = 0;
        }

        internal void Tick(double realtime)
        {
            if (server == null) return;
            bool changed = false;
            while (server.TryTakeFlightChange(out var change))
            {
                // §5.4: runs in DevRuntimeHost.Update, between world updates.
                if (flight.TryApply(context?.World, change.Name, change.Value, change.Reset, out string error)) flightError = null;
                else flightError = error;
                changed = true;
            }
            if (!changed && (realtime < nextBuild || !server.PolledWithin(PollWindow))) return;
            nextBuild = realtime + BuildInterval;
            Publish();
        }

        void OnBindingsChanged() => layoutDirty = true;   // rebinds and the keyboard/gamepad switch both relabel controls

        void Publish()
        {
            var asset = input.Controls.asset;
            if (layoutDirty)
            {
                layoutDirty = false; layoutVersion++;
                server.Layout = Encoding.UTF8.GetBytes(ControlSheetJson.Layout(asset, bindingLabel, layoutVersion));
            }
            bool flightAvailable = flight.TryRead(context?.World, out var current, out var original);
            server.Flight = Encoding.UTF8.GetBytes(DevFlightConfig.Json(flightAvailable, current, original, flightError));
            ParseProbeQuery(server.ProbeQuery);
            server.State = Encoding.UTF8.GetBytes(ControlSheetJson.State(asset, layoutVersion, input.CurrentContext.ToString(), input.CurrentControlScheme, probeNames, probeJson));
        }

        void ParseProbeQuery(string query)
        {
            if (query == parsedProbeQuery) return;
            parsedProbeQuery = query;
            probeNames.Clear();
            foreach (string part in query.Split(','))
            {
                string name = part.Trim();
                if (name.Length > 0 && probeNames.Count < MaxProbes && !probeNames.Contains(name)) probeNames.Add(name);
            }
        }

        /// <summary>A zero-argument scenario probe as a JSON value; null when the name is unknown, takes arguments or cannot read now.</summary>
        string ReadProbe(string name)
        {
            if (probes == null || !probes.TryGet(name, out var probe) || probe.ArgCount != 0) return "null";
            try
            {
                var value = probe.Read(context, ReadOnlySpan<ScenarioValue>.Empty);
                switch (value.Kind)
                {
                    case ScenarioValueKind.Number: return DevJsonWriter.Number(value.Number);
                    case ScenarioValueKind.Bool: return value.Bool ? "true" : "false";
                    default: return DevJsonWriter.Quote(value.String);
                }
            }
            catch (Exception) { return "null"; }        // no world, no player: the page shows a dash
        }

        /// <summary>The nearest tools/controlsheet with an index.html, from <paramref name="dataPath"/> upwards (the Editor's Assets, or a build inside the project).</summary>
        internal static string FindPageRoot(string dataPath)
        {
            var dir = string.IsNullOrEmpty(dataPath) ? null : new DirectoryInfo(dataPath);
            for (int i = 0; i < 5 && dir != null; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "tools", "controlsheet");
                if (File.Exists(Path.Combine(candidate, "index.html"))) return candidate;
            }
            return null;
        }

        public void Dispose() => Close();
    }
}
#endif
