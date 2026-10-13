namespace Game.Simulation.Factions
{
    /// <summary>SPEC-33 §4.2 main-thread relation requests; ENC-003 consumes them in order.</summary>
    [Unity.Entities.InternalBufferCapacity(0)]
    public struct FactionRelationRequest : Unity.Entities.IBufferElementData
    {
        public byte Faction, Mode;
        public ushort Reserved;
        public int Value;
    }
}
