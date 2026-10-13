namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 §4.5 immutable quest entry read model.</summary>
    public readonly struct QuestEntryView { public readonly int QuestIndex; public readonly string QuestId, Title, StageText, ShortDescription;
        public readonly Game.Data.Logic.QuestStatus Status; public readonly bool Tracked; public readonly int Instance;
        public QuestEntryView(int i, string id, string title, string stage, string shortDesc, Game.Data.Logic.QuestStatus st, bool tracked, int inst)
        { QuestIndex = i; QuestId = id; Title = title; StageText = stage; ShortDescription = shortDesc; Status = st; Tracked = tracked; Instance = inst; } }
}
