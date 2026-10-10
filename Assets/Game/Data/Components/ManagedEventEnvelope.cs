namespace Game.Data.Components
{
    /// <summary>One ECS-produced managed event (SPEC-02 §4.6, §5.6).</summary>
    public struct ManagedEventEnvelope<T> where T : unmanaged
    {
        public uint Tick;          // SimClock.Tick when produced (0 if no SimClock)
        public ulong SortKey;      // producer-chosen stable key, normally PersistentId.Value of the subject
        public T Payload;          // unmanaged payload type from Game.Core.Events
    }
}
