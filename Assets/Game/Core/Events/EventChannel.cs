using System;
using System.Collections.Generic;

namespace Game.Core.Events
{
    /// <summary>One event type's handlers and pending queue inside EventHub (internal, SPEC-02 §4.5, §5.5).</summary>
    internal interface IEventChannel
    {
        int Id { get; set; }
        bool DispatchIfHead(long sequence);
        void Reset();
        int PendingCount { get; }
    }

    /// <summary>Channel of one payload type (internal, SPEC-02 §4.5, §5.5). Main thread only.</summary>
    internal sealed class EventChannel<T> : IEventChannel where T : struct
    {
        internal struct Entry { public long Sequence; public T Value; }

        internal Action<T>[] handlers = Array.Empty<Action<T>>();   // copy-on-write: dispatch iterates a snapshot
        internal readonly Queue<Entry> queue = new Queue<Entry>(16);
        readonly Action detach;

        internal EventChannel(Action detach) { this.detach = detach; }

        public int Id { get; set; }
        public int PendingCount => queue.Count;

        internal void Subscribe(Action<T> h)
        {
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (Array.IndexOf(handlers, h) >= 0)
            {
                GameLog.WarnOnce("EventBus.DuplicateSubscribe." + typeof(T).FullName, LogCategories.Core, $"duplicate subscription to EventBus<{typeof(T).Name}> ignored");
                return;
            }
            var next = new Action<T>[handlers.Length + 1];
            Array.Copy(handlers, next, handlers.Length);
            next[handlers.Length] = h;
            handlers = next;
        }

        internal void Unsubscribe(Action<T> h)
        {
            int i = Array.IndexOf(handlers, h);
            if (i < 0) return;
            var next = new Action<T>[handlers.Length - 1];
            Array.Copy(handlers, 0, next, 0, i);
            Array.Copy(handlers, i + 1, next, i, handlers.Length - i - 1);
            handlers = next;
        }

        /// <summary>Delivers the queue head if it is the event with this sequence; false when it was already delivered by EventBus&lt;T&gt;.Drain.</summary>
        public bool DispatchIfHead(long sequence)
        {
            if (queue.Count == 0 || queue.Peek().Sequence != sequence) return false;
            Invoke(queue.Dequeue().Value);
            return true;
        }

        /// <summary>Delivers every queued entry in order (EventBus&lt;T&gt;.Drain); their hub references become stale.</summary>
        internal void DrainOwn()
        {
            while (queue.Count > 0) Invoke(queue.Dequeue().Value);
        }

        void Invoke(T value)
        {
            var snapshot = handlers;
            for (int i = 0; i < snapshot.Length; i++)
            {
                var h = snapshot[i];
                if (!ReferenceEquals(handlers, snapshot) && Array.IndexOf(handlers, h) < 0) continue;   // unsubscribed during this dispatch
                try { h(value); }
                catch (Exception ex) { GameLog.Exception(LogCategories.Core, ex, "EventBus<" + typeof(T).Name + ">"); }
            }
        }

        public void Reset()
        {
            handlers = Array.Empty<Action<T>>();
            queue.Clear();
            detach();
        }
    }
}
