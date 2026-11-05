#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace Game.App.DevRuntime.ControlSheet
{
    /// <summary>Serves the control sheet page and its JSON on 127.0.0.1 (SPEC-70 DEV-029). Its thread never calls Unity:
    /// it reads only files under the page root and the bytes the main thread last published.</summary>
    internal sealed class ControlSheetServer : IDisposable
    {
        const int MaxProbeQuery = 2048, MaxQueuedChanges = 256;
        static readonly byte[] EmptyJson = Encoding.UTF8.GetBytes("{}");
        readonly string pageRoot;
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        HttpListener listener;
        Thread thread;
        volatile byte[] layout = EmptyJson, state = EmptyJson, flight = EmptyJson;
        readonly ConcurrentQueue<FlightChange> flightChanges = new ConcurrentQueue<FlightChange>();
        volatile string probeQuery = string.Empty;
        long lastPoll;                                  // Stopwatch timestamp of the last /state request; 0 = never
        bool disposed;

        internal ControlSheetServer(string pageRoot) { this.pageRoot = Path.GetFullPath(pageRoot); }

        internal int Port { get; private set; }
        /// <summary>The page's comma-separated <c>?probes=</c> list from its last /state request.</summary>
        internal string ProbeQuery => probeQuery;
        internal byte[] Layout { set => layout = value ?? EmptyJson; }
        internal byte[] State { set => state = value ?? EmptyJson; }
        internal byte[] Flight { set => flight = value ?? EmptyJson; }

        /// <summary>A Flight configs edit posted by the page, applied by the main thread (DEV-030). Reset with an empty name resets every field.</summary>
        internal readonly struct FlightChange
        {
            internal readonly string Name; internal readonly double Value; internal readonly bool Reset;
            internal FlightChange(string name, double value, bool reset) { Name = name; Value = value; Reset = reset; }
        }
        internal bool TryTakeFlightChange(out FlightChange change) => flightChanges.TryDequeue(out change);

        internal bool TryStart(int port, out string error)
        {
            error = null;
            var candidate = new HttpListener();
            try
            {
                candidate.Prefixes.Add("http://127.0.0.1:" + port + "/");    // loopback only; the prefix also rejects other Host headers
                candidate.Start();
            }
            catch (Exception e) { candidate.Close(); error = "port " + port + ": " + e.Message; return false; }
            listener = candidate; Port = port;
            thread = new Thread(Serve) { IsBackground = true, Name = "ControlSheetServer" };
            thread.Start();
            return true;
        }

        internal bool PolledWithin(double seconds)
        {
            long last = Interlocked.Read(ref lastPoll);
            return last != 0 && (Stopwatch.GetTimestamp() - last) < seconds * Stopwatch.Frequency;
        }

        void Serve()
        {
            var handles = new WaitHandle[2];
            handles[1] = stop;
            while (true)
            {
                IAsyncResult pending;
                try { pending = listener.BeginGetContext(null, null); }
                catch (Exception) { return; }                                  // closed
                handles[0] = pending.AsyncWaitHandle;
                if (WaitHandle.WaitAny(handles) == 1) return;
                HttpListenerContext context;
                try { context = listener.EndGetContext(pending); }
                catch (Exception) { return; }
                try { Handle(context); }
                catch (Exception) { }                                          // a dropped connection must not end the server
                finally { try { context.Response.Close(); } catch (Exception) { } }
            }
        }

        void Handle(HttpListenerContext context)
        {
            var response = context.Response;
            response.Headers["Cache-Control"] = "no-store";
            string path = context.Request.Url.AbsolutePath;
            if (context.Request.HttpMethod == "POST") { HandlePost(context, path); return; }
            if (context.Request.HttpMethod != "GET") { response.StatusCode = 405; return; }
            if (path == "/layout") { Send(response, "application/json", layout); return; }
            if (path == "/flight") { Send(response, "application/json", flight); return; }
            if (path == "/state")
            {
                string probes = context.Request.QueryString["probes"] ?? string.Empty;
                probeQuery = probes.Length > MaxProbeQuery ? probes.Substring(0, MaxProbeQuery) : probes;
                Interlocked.Exchange(ref lastPoll, Stopwatch.GetTimestamp());
                Send(response, "application/json", state);
                return;
            }
            string file = ResolveFile(pageRoot, path);
            string type = file == null ? null : ContentType(file);
            if (type == null || !File.Exists(file)) { response.StatusCode = 404; return; }
            Send(response, type, File.ReadAllBytes(file));                    // read per request: edit the page, refresh, no recompile
        }

        void HandlePost(HttpListenerContext context, string path)
        {
            var response = context.Response;
            // A cross-site page can post plain forms to localhost but cannot add this header without a CORS preflight, which is never granted.
            if (context.Request.Headers["X-Control-Sheet"] != "1") { response.StatusCode = 403; return; }
            if (flightChanges.Count >= MaxQueuedChanges) { response.StatusCode = 503; return; }
            string name = context.Request.QueryString["name"] ?? string.Empty;
            if (path == "/flight/reset") flightChanges.Enqueue(new FlightChange(name, 0, true));
            else if (path == "/flight" && name.Length > 0 &&
                     double.TryParse(context.Request.QueryString["value"], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                flightChanges.Enqueue(new FlightChange(name, value, false));
            else { response.StatusCode = path == "/flight" ? 400 : 404; return; }
            response.StatusCode = 202;                                         // applied by the main thread next frame
            Send(response, "application/json", EmptyJson);
        }

        static void Send(HttpListenerResponse response, string type, byte[] body)
        {
            response.ContentType = type;
            response.ContentLength64 = body.Length;
            response.OutputStream.Write(body, 0, body.Length);
        }

        /// <summary>The file under <paramref name="root"/> a URL path names, or null for anything that could leave it.</summary>
        internal static string ResolveFile(string root, string urlPath)
        {
            string relative = urlPath == "/" ? "index.html" : urlPath.TrimStart('/');
            if (relative.Length == 0 || relative.Contains("..")) return null;
            foreach (char c in relative)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == '/')) return null;
            string full = Path.GetFullPath(Path.Combine(root, relative));
            string prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString()) ? root : root + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? full : null;
        }

        static string ContentType(string file)
        {
            switch (Path.GetExtension(file).ToLowerInvariant())
            {
                case ".html": return "text/html; charset=utf-8";
                case ".js": return "text/javascript; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".json": return "application/json";
                case ".svg": return "image/svg+xml";
                case ".png": return "image/png";
                default: return null;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            stop.Set();
            if (listener != null) { try { listener.Stop(); listener.Close(); } catch (Exception) { } }
            // A thread still inside a request after the join keeps the event; it exits on the set event or the closed listener.
            if (thread == null || thread.Join(2000)) stop.Dispose();
            listener = null; thread = null;
        }
    }
}
#endif
