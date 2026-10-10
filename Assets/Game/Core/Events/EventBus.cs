namespace Game.Core.Events
{
    /// <summary>Typed deferred event bus (architecture §6.1, SPEC-02 §4.5, §5.5). Main thread only.</summary>
    public static class EventBus<T> where T : struct
    {
        public static void Subscribe(System.Action<T> handler) { throw new System.NotImplementedException("CORE-006"); }     // duplicate subscription ignored with WarnOnce
        public static void Unsubscribe(System.Action<T> handler) { throw new System.NotImplementedException("CORE-006"); }   // safe at any time, including inside a handler
        public static void Publish(in T evt) { throw new System.NotImplementedException("CORE-006"); }                       // deferred until the next EventHub.DrainAll()
        public static void Drain() { throw new System.NotImplementedException("CORE-006"); }                                 // delivers only T's pending events (tests); no-op + WarnOnce inside DrainAll
        public static int SubscriberCount => throw new System.NotImplementedException("CORE-006");
        public static int PendingCount => throw new System.NotImplementedException("CORE-006");
    }
}
