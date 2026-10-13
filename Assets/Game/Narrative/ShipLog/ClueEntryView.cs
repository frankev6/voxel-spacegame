namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 §4.5 immutable clue entry read model.</summary>
    public readonly struct ClueEntryView { public readonly int ClueIndex; public readonly string ClueId, Title, Body, MysteryTitle; public readonly int Ordinal; public readonly bool IsNew, IsRumor, Resolved; public readonly Game.Core.ContentId TargetStarSystem;
        public ClueEntryView(int i, string id, string title, string body, string mystery, int ord, bool isNew, bool rumor, bool resolved, Game.Core.ContentId target)
        { ClueIndex = i; ClueId = id; Title = title; Body = body; MysteryTitle = mystery; Ordinal = ord; IsNew = isNew; IsRumor = rumor; Resolved = resolved; TargetStarSystem = target; } }
}
