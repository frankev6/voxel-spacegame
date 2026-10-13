namespace Game.Simulation.Factions
{
    /// <summary>SPEC-33 §4.2 faction singleton thresholds and revision; writer follows in ENC-003.</summary>
    public struct FactionState : Unity.Entities.IComponentData
    {
        public int HostileAtOrBelow, FriendlyAtOrAbove, ProvokePenalty, ProvokeIntervalTicks;
        public int Count;
        public uint Revision;
    }
}
