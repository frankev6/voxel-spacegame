namespace Game.Galaxy.Encounters
{
    /// <summary>SPEC-33 §4.4 immutable managed instance view for hails, debug and tests.</summary>
    public readonly struct EncounterInstanceInfo
    {
        public readonly uint Id;
        public readonly Game.Data.Definitions.EncounterDefinition Definition;
        public readonly Game.Galaxy.Data.StarSystemId StarSystem;
        public readonly EncounterSource Source;
        public readonly InstanceState State;
        public readonly int Members, AliveMembers;
        public readonly bool Persistent;
        public EncounterInstanceInfo(uint id, Game.Data.Definitions.EncounterDefinition def, Game.Galaxy.Data.StarSystemId sys,
            EncounterSource src, InstanceState st, int members, int alive, bool persistent)
        { Id = id; Definition = def; StarSystem = sys; Source = src; State = st; Members = members; AliveMembers = alive; Persistent = persistent; }
    }
}
