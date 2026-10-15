using System.Collections.Generic;
using Game.Simulation.Commands;

namespace Game.Net.Replication
{
    /// <summary>One guest tick's input: the sampled command plus the guest's own target lock (by network id) and weapon mode, which
    /// the guest owns as HUD state (SPEC-90 §10a; the host's targeting system runs only for its own ship).</summary>
    public struct PilotCommand
    {
        public PlayerCommand Command;
        public ulong Target;
        public byte Autofire, AimMode;
        public ulong AimAnchor; public Unity.Mathematics.float3 AimLocal;   // AimAnchor.cs: aim as a point on a grid, 0 = open space
    }

    /// <summary>Host side of NET-011: one guest's commands, one per guest fixed tick, played back one per host fixed tick so the host
    /// applies each command for exactly one tick, as the guest's prediction did. An empty queue repeats the last command (the
    /// acknowledged sequence does not advance). An adaptive jitter buffer: a starve raises the depth the queue keeps, a quiet stretch
    /// lowers it again, and standing surplus above it is dropped. Each starve or skip costs the guest a one-off correction instead of
    /// standing latency. Main thread.</summary>
    public sealed class PilotCommandQueue
    {
        public const int MaxDepth = 12, MinTarget = 1, MaxTarget = 6, WindowTicks = 120;
        readonly Queue<(uint Sequence, PilotCommand Command)> queue = new();
        uint newestQueued;
        bool any;
        int windowTicks, windowMin = int.MaxValue, windowStarves;

        public uint Applied { get; private set; }
        public PilotCommand Current { get; private set; }
        public bool HasCommand => any;
        public int Depth => queue.Count;
        public int Target { get; private set; } = 2;   // commands kept waiting before this tick's pop
        public int Starved { get; private set; }
        public int Skipped { get; private set; }

        /// <summary>Adds a received command; repeats and anything older than the newest queued are ignored (packets carry redundancy).</summary>
        public void Push(uint sequence, in PilotCommand command)
        {
            if (any && unchecked((int)(sequence - newestQueued)) <= 0) return;
            if (!any) { Applied = sequence - 1; Current = command; }
            newestQueued = sequence; any = true;
            queue.Enqueue((sequence, command));
        }

        /// <summary>Command for the next host tick.</summary>
        public void Pop()
        {
            if (!any) return;
            if (queue.Count > MaxDepth) Drop(queue.Count - Target);
            windowMin = System.Math.Min(windowMin, queue.Count);
            if (++windowTicks >= WindowTicks)
            {
                // Two quiet seconds: try a shallower buffer. Then drop what stood above the target the whole window.
                if (windowStarves == 0 && Target > MinTarget) Target--;
                if (windowStarves == 0 && windowMin > Target) Drop(windowMin - Target);
                windowTicks = 0; windowMin = int.MaxValue; windowStarves = 0;
            }
            if (queue.Count == 0)
            {
                Starved++; windowStarves++;
                if (windowStarves == 1 && Target < MaxTarget) Target++;
                return;
            }
            var next = queue.Dequeue();
            Applied = next.Sequence; Current = next.Command;
        }

        void Drop(int n)
        {
            for (; n > 0 && queue.Count > 1; n--) { queue.Dequeue(); Skipped++; }
        }
    }
}
