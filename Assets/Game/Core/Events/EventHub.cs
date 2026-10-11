using System.Collections.Generic;

namespace Game.Core.Events
{
    /// <summary>Global drain point for every EventBus&lt;T&gt; (SPEC-02 §4.5, §5.5). Main thread only. Events of all types are delivered in
    /// publish order within a pass; events published by handlers follow in the next pass, at most MaxCascadePasses per DrainAll.</summary>
    public static class EventHub
    {
        public const int MaxCascadePasses = 8;

        internal struct PendingRef { public int Channel; public long Sequence; }

        static List<IEventChannel> channels = new List<IEventChannel>();
        static List<PendingRef> pending = new List<PendingRef>(256);
        static List<PendingRef> processing = new List<PendingRef>(256);
        static long sequence;
        static bool draining;

        internal static EventChannel<T> Register<T>(EventChannel<T> channel) where T : struct
        {
            channel.Id = channels.Count;
            channels.Add(channel);
            return channel;
        }

        internal static void Enqueue(int channel, long seq) { pending.Add(new PendingRef { Channel = channel, Sequence = seq }); }
        internal static long NextSequence() => ++sequence;

        /// <summary>Delivers all pending events in publish order (§5.5 step 4).</summary>
        public static void DrainAll()
        {
            if (draining) { GameLog.WarnOnce("EventHub.Reentrant", LogCategories.Core, "EventHub.DrainAll called during a drain; ignored"); return; }
            draining = true;
            try
            {
                for (int pass = 0; pass < MaxCascadePasses; pass++)
                {
                    if (pending.Count == 0) break;
                    (pending, processing) = (processing, pending);
                    for (int i = 0; i < processing.Count; i++)
                    {
                        var r = processing[i];
                        if (r.Channel < channels.Count) channels[r.Channel].DispatchIfHead(r.Sequence);
                    }
                    processing.Clear();
                }
                if (pending.Count > 0)
                    GameLog.WarnOnce("EventHub.Cascade", LogCategories.Core, "events still pending after 8 passes; delivered next frame");
            }
            finally { draining = false; }
        }

        public static bool IsDraining => draining;
        /// <summary>Registered channels (one per payload type in use); diagnostics and the Play Mode re-entry test.</summary>
        internal static int ChannelCount => channels.Count;
        public static int PendingCount => pending.Count;                    // pending entries across all types (stale entries included)

        /// <summary>SubsystemRegistration and tests: clears all buses, handlers and pending events.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            foreach (var c in channels) c.Reset();
            channels.Clear();
            pending.Clear();
            processing.Clear();
            sequence = 0;
            draining = false;
        }
    }
}
