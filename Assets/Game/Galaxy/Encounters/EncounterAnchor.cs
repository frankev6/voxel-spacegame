namespace Game.Galaxy.Encounters
{
    /// <summary>SPEC-33 §4.4 managed player/body/story-tag anchor; never an ECS component.</summary>
    public struct EncounterAnchor
    {
        public EncounterAnchorKind Kind;
        public Game.Galaxy.Data.BodyId Body;
        public string Tag;
        public static EncounterAnchor Player() => new EncounterAnchor { Kind = EncounterAnchorKind.Player };
    }
}
