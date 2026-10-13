using System;
using Game.Data.Logic;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §5.1 lane cache; disposal preserves the revision stamp.</summary>
    internal sealed class LaneAccessTable : IDisposable
    {
        public uint Revision;
        public int BuiltForGeneration = -1;
        public bool[] GateOpen = Array.Empty<bool>();
        public LogicWatcher[] Watchers = Array.Empty<LogicWatcher>();
        public bool PendingContext;
        public void Dispose()
        {
            foreach (var watcher in Watchers) watcher?.Dispose();
            Watchers = Array.Empty<LogicWatcher>(); GateOpen = Array.Empty<bool>();
            PendingContext = false; BuiltForGeneration = -1;
        }
    }
}
