namespace Game.Core
{
    /// <summary>Managed PersistentId source, implemented by GameSession (SPEC-01). Main thread only. Implements SPEC-02 §4.1.</summary>
    public interface IPersistentIdAllocator
    {
        ulong Allocate();                                  // never returns 0; never returns a value twice in a campaign
    }
}
