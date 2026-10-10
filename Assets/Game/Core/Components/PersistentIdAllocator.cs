namespace Game.Core.Components
{
    /// <summary>Singleton block of pre-granted PersistentIds for Burst systems (SPEC-13 hulks, SPEC-31 spawns).
    /// GameSession (SPEC-01 §5) refills it on the main thread; Burst systems take ids with Next++ while Next &lt; End.
    /// Implements SPEC-02 §4.1.</summary>
    public struct PersistentIdAllocator : Unity.Entities.IComponentData
    {
        public ulong Next;                                 // next id to hand out
        public ulong End;                                  // exclusive; Next == End means empty (consumer defers its work)
    }
}
