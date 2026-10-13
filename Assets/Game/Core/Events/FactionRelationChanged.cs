namespace Game.Core.Events
{
    /// <summary>SPEC-33 §4.3 ECS-produced faction change (ENC-003).</summary>
    public readonly struct FactionRelationChanged
    {
        public readonly Game.Core.ContentHash Faction;
        public readonly int OldRelation, NewRelation;
        public readonly byte OldStance, NewStance;
        public FactionRelationChanged(Game.Core.ContentHash f, int oldR, int newR, byte oldS, byte newS)
        { Faction = f; OldRelation = oldR; NewRelation = newR; OldStance = oldS; NewStance = newS; }
    }
}
