using System;

namespace Game.Narrative
{
    /// <summary>SPEC-40 §4.10: bounded chronological developer trace; no storage in release builds.</summary>
    public sealed class LogicTraceLog
    {
        public const int Capacity = 512;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        readonly LogicTraceEntry[] entries = new LogicTraceEntry[Capacity];
        int next, count;
#endif
        public int Version { get; private set; }
        public void Add(in LogicTraceEntry e)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            entries[next] = e; next = (next + 1) % Capacity;
            if (count < Capacity) count++;
            unchecked { Version++; }
#endif
        }
        public int CopyTo(LogicTraceEntry[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int copied = Math.Min(count, buffer.Length), start = (next - count + Capacity) % Capacity;
            for (int i = 0; i < copied; i++) buffer[i] = entries[(start + i) % Capacity];
            return copied;
#else
            return 0;
#endif
        }
    }
}
