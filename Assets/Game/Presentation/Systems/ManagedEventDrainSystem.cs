namespace Game.Presentation.Systems
{
    /// <summary>Moves ECS-produced events into EventBus&lt;T&gt; and drains EventHub once per frame (architecture §5, SPEC-02 §4.6, §5.6).</summary>
    [Unity.Entities.UpdateInGroup(typeof(Game.Core.Systems.PresentationBridgeGroup), OrderLast = true)]
    public partial class ManagedEventDrainSystem : Unity.Entities.SystemBase
    {
        public void RegisterChannel<T>() where T : unmanaged { throw new System.NotImplementedException("CORE-009"); }   // idempotent; main thread; creates the singleton entity
        public int ChannelCount => throw new System.NotImplementedException("CORE-009");

        // The lifecycle methods stay empty until CORE-009 (not NotImplementedException): this system runs in every world, and a
        // throwing OnUpdate would log an error each frame in Play Mode (SPEC-02 open question 10).
        protected override void OnCreate() { ManagedEventChannels.RegisterAll(this); }   // TODO(CORE-009): create the channel list first
        protected override void OnUpdate() { }                                            // TODO(CORE-009): transfer all channels, then EventHub.DrainAll()
        protected override void OnDestroy() { }                                           // TODO(CORE-009): complete dependencies, dispose every queue
    }

    /// <summary>Transfers and disposes one channel's queue (internal, SPEC-02 §4.6, §5.6).</summary>
    interface IManagedEventChannel
    {
        void Transfer(Unity.Entities.EntityManager em);
        void Dispose(Unity.Entities.EntityManager em);
    }

    /// <summary>Channel of one payload type, holding the query of its singleton (internal, SPEC-02 §4.6, §5.6).</summary>
    sealed class ManagedEventChannel<T> : IManagedEventChannel where T : unmanaged
    {
        public void Transfer(Unity.Entities.EntityManager em) { throw new System.NotImplementedException("CORE-009"); }
        public void Dispose(Unity.Entities.EntityManager em) { throw new System.NotImplementedException("CORE-009"); }
    }

    /// <summary>Orders a frame's batch by (Tick, SortKey, payload hash) (internal, SPEC-02 §5.6 step 4).</summary>
    struct EnvelopeComparer<T> : System.Collections.Generic.IComparer<Game.Data.Components.ManagedEventEnvelope<T>> where T : unmanaged
    {
        public int Compare(Game.Data.Components.ManagedEventEnvelope<T> x, Game.Data.Components.ManagedEventEnvelope<T> y) { throw new System.NotImplementedException("CORE-009"); }
    }
}
