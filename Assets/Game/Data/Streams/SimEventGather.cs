namespace Game.Data.Streams
{
    /// <summary>Burst helper for consumers: appends every event of a read set to output, registrations in order, foreach indices ascending.
    /// Consumers then stable-sort with their own key (the gather order is the tie-break). Implements SPEC-02 §4.12, §5.13.</summary>
    public static class SimEventGather
    {
        public static Unity.Jobs.JobHandle Schedule<T>(SimEventReadSet set, Unity.Collections.NativeList<T> output, Unity.Jobs.JobHandle dependsOn) where T : unmanaged { throw new System.NotImplementedException("CORE-012"); }
    }
}
