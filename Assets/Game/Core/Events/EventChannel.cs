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

    /// <summary>Channel of one payload type (internal, SPEC-02 §4.5, §5.5).</summary>
    internal sealed class EventChannel<T> : IEventChannel where T : struct
    {
        internal struct Entry { public long Sequence; public T Value; }

        // CORE-006 adds the state of §4.5: System.Action<T>[] handlers (copy-on-write), Queue<Entry> queue, System.Action detach.
        // Declaring them unused here would add compiler warnings.

        public int Id { get => throw new System.NotImplementedException("CORE-006"); set => throw new System.NotImplementedException("CORE-006"); }
        public bool DispatchIfHead(long sequence) { throw new System.NotImplementedException("CORE-006"); }
        public void Reset() { throw new System.NotImplementedException("CORE-006"); }
        public int PendingCount => throw new System.NotImplementedException("CORE-006");
    }
}
