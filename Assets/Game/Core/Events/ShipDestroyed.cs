namespace Game.Core.Events
{
    /// <summary>A ship or station lost its core; its remaining hulk is not another destruction (SPEC-21 §4.6b).</summary>
    public readonly struct ShipDestroyed
    {
        public readonly ulong PersistentId;
        public readonly ContentHash Blueprint;
        public readonly ContentHash Encounter;
        public readonly uint EncounterInstance;
        public readonly uint Tick;
        public readonly byte IsPlayer;
        public readonly byte Faction;
        public readonly byte Kind;
        public readonly ulong KillerPersistentId;
        public readonly byte KillerIsPlayer;

        public ShipDestroyed(ulong persistentId, ContentHash blueprint, ContentHash encounter, uint encounterInstance,
            uint tick, byte isPlayer, byte faction, byte kind, ulong killerPersistentId, byte killerIsPlayer)
        {
            PersistentId = persistentId;
            Blueprint = blueprint;
            Encounter = encounter;
            EncounterInstance = encounterInstance;
            Tick = tick;
            IsPlayer = isPlayer;
            Faction = faction;
            Kind = kind;
            KillerPersistentId = killerPersistentId;
            KillerIsPlayer = killerIsPlayer;
        }
    }
}
