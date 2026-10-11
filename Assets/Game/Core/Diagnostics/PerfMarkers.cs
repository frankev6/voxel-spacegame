using System.Collections.Generic;

namespace Game.Core.Diagnostics
{
    /// <summary>Recorders for game profiler markers (SPEC-71 decision 7, §5.3). Markers are discovered, not registered: every available
    /// marker in ProfilerCategory.Scripts named "&lt;Type&gt;.&lt;Step&gt;" (architecture §11.11). Dev builds and tests only: in a
    /// release build every call is a no-op.</summary>
    public static class PerfMarkers
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly System.Text.RegularExpressions.Regex NamePattern = new System.Text.RegularExpressions.Regex(@"^[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$");
        static readonly Dictionary<string, Unity.Profiling.ProfilerRecorder> recorders = new Dictionary<string, Unity.Profiling.ProfilerRecorder>();
        static readonly List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle> handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
        static readonly List<(string name, double ms)> scratch = new List<(string name, double ms)>();
#endif

        /// <summary>Idempotent: a second call only adds markers that appeared since (systems created later).</summary>
        public static void StartRecording(int capacityFrames = 1)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            handles.Clear();
            Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var h in handles)
            {
                var d = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h);
                if (d.Category != Unity.Profiling.ProfilerCategory.Scripts) continue;
                if (recorders.ContainsKey(d.Name) || !NamePattern.IsMatch(d.Name)) continue;
                recorders.Add(d.Name, Unity.Profiling.ProfilerRecorder.StartNew(d.Category, d.Name, capacityFrames));
            }
#endif
        }

        /// <summary>Disposes every recorder.</summary>
        public static void StopRecording()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var r in recorders.Values) r.Dispose();
            recorders.Clear();
#endif
        }

        public static int Count
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return recorders.Count;
#else
                return 0;
#endif
            }
        }

        /// <summary>Fills <paramref name="into"/> with (name, main-thread ms of the last frame), sorted descending; at most <paramref name="max"/> entries.</summary>
        public static void SampleLastFrame(List<(string name, double ms)> into, int max)
        {
            into.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            scratch.Clear();
            foreach (var kv in recorders) scratch.Add((kv.Key, kv.Value.LastValue * 1e-6));
            scratch.Sort((a, b) => b.ms.CompareTo(a.ms));
            for (int i = 0; i < scratch.Count && i < max; i++) into.Add(scratch[i]);
#endif
        }

        public static bool TryGetLastFrameMs(string markerName, out double ms)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (recorders.TryGetValue(markerName, out var r)) { ms = r.LastValue * 1e-6; return true; }
#endif
            ms = 0;
            return false;
        }

        /// <summary>Main-thread ms recorded so far in the current frame (EditMode loops that never end a frame; SPEC-71 open question 5).</summary>
        internal static bool TryGetCurrentFrameMs(string markerName, out double ms)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (recorders.TryGetValue(markerName, out var r)) { ms = r.CurrentValue * 1e-6; return true; }
#endif
            ms = 0;
            return false;
        }

        internal static IEnumerable<string> Names
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return recorders.Keys;
#else
                return System.Array.Empty<string>();
#endif
            }
        }
    }
}
