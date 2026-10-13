namespace Game.Galaxy.Encounters
{
    /// <summary>SPEC-33 §4.4 internal managed instance state; ENC-007 owns lifecycle.</summary>
    internal sealed class EncounterInstance
    {
        public uint Id = default;
        public Game.Data.Definitions.EncounterDefinition Definition = default;
        public Game.Galaxy.Data.StarSystemId StarSystem = default;
        public EncounterSource Source = default;
        public InstanceState State = default;
        public bool Persistent = default;
        public ulong Seed = default;
        public EncounterAnchor Anchor = default;
        public readonly System.Collections.Generic.List<ulong> Members = new(10);
        public int AliveMembers = default;
        public double FarSinceUniversalTime = -1;
        public bool AnyEngaging = default;
    }
}
