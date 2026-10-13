namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 §4.5 immutable objective or rumor map marker.</summary>
    public readonly struct NarrativeMarker
    {
        public readonly NarrativeMarkerKind Kind; public readonly Game.Core.ContentId StarSystem; public readonly string BodyTag; public readonly string Label;
        public readonly int QuestIndex; public readonly int ClueIndex;
        public NarrativeMarker(NarrativeMarkerKind k, Game.Core.ContentId sys, string tag, string label, int quest, int clue)
        { Kind = k; StarSystem = sys; BodyTag = tag; Label = label; QuestIndex = quest; ClueIndex = clue; }
    }
}
