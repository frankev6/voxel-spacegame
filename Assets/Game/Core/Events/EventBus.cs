namespace Game.Core.Events
{
    /// <summary>Typed deferred event bus (architecture §6.1, SPEC-02 §4.5, §5.5). Main thread only. Publish never calls a handler;
    /// delivery happens in EventHub.DrainAll (once per frame) or EventBus&lt;T&gt;.Drain (tests).</summary>
    public static class EventBus<T> where T : struct
    {
        static EventChannel<T> channel;   // created on first use; EventHub.Reset detaches it

        static EventChannel<T> Channel => channel ??= EventHub.Register(new EventChannel<T>(DetachChannel));
        static void DetachChannel() { channel = null; }

        public static void Subscribe(System.Action<T> handler) { Channel.Subscribe(handler); }      // duplicate subscription ignored with WarnOnce
        public static void Unsubscribe(System.Action<T> handler) { channel?.Unsubscribe(handler); } // safe at any time, including inside a handler

        /// <summary>Deferred until the next EventHub.DrainAll(). 0 B after the queues have grown.</summary>
        public static void Publish(in T evt)
        {
            var c = Channel;
            long s = EventHub.NextSequence();
            c.queue.Enqueue(new EventChannel<T>.Entry { Sequence = s, Value = evt });
            EventHub.Enqueue(c.Id, s);
        }

        /// <summary>Delivers only T's pending events (tests); no-op + WarnOnce inside DrainAll.</summary>
        public static void Drain()
        {
            if (EventHub.IsDraining) { GameLog.WarnOnce("EventBus.DrainInsideDrainAll." + typeof(T).FullName, LogCategories.Core, $"EventBus<{typeof(T).Name}>.Drain called inside EventHub.DrainAll; ignored"); return; }
            channel?.DrainOwn();
        }

        public static int SubscriberCount => channel?.handlers.Length ?? 0;
        public static int PendingCount => channel?.PendingCount ?? 0;
    }
}
