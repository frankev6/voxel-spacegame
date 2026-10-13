namespace Game.Simulation.Factions
{
    /// <summary>SPEC-33 §4.2 per-faction standing; ENC-003 is the fixed-step writer, ENC-004 owns restore/new game.</summary>
    [Unity.Entities.InternalBufferCapacity(0)]
    public struct FactionStanding : Unity.Entities.IBufferElementData
    {
        public Game.Core.ContentHash Faction;
        public int Relation;
        public byte Locked;
        public FactionStance PlayerStance;
        public ushort Reserved;
        public uint LastProvokedTick;
    }
}
