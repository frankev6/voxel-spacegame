namespace Game.Data.Components
{
    /// <summary>Singleton queue for one managed event type (SPEC-02 §4.6, §5.6). Created and disposed by ManagedEventDrainSystem.</summary>
    public struct ManagedEventQueue<T> : Unity.Entities.IComponentData where T : unmanaged
    {
        public Unity.Collections.NativeQueue<ManagedEventEnvelope<T>> Queue;   // Allocator.Persistent
    }
}
