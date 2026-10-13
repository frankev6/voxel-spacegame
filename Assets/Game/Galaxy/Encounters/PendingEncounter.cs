namespace Game.Galaxy.Encounters
{
    /// <summary>SPEC-33 §4.4 internal managed pending/save data; ENC-007/008 own behavior.</summary>
    internal struct PendingEncounter
    {
        public Game.Core.ContentId Encounter;
        public uint InstanceId;
        public Game.Galaxy.Data.StarSystemId StarSystem;
        public EncounterAnchor Anchor;
        public bool Combat;
    }
}
