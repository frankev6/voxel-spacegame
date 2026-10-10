namespace Game.Core.Components
{
    /// <summary>Simulation clock singleton, on the same entity as GameplayActive (SPEC-02 §4.3, §5.9).</summary>
    public struct SimClock : Unity.Entities.IComponentData
    {
        public uint Tick;                          // completed fixed ticks this session; 0 during the first tick
        public double UniversalTime;               // seconds since campaign start at the start of the current tick
        public double SessionStartUniversalTime;   // UniversalTime when this session's gameplay began (from the save)
    }
}
