namespace Game.Narrative.Quests
{
    /// <summary>SPEC-42 §4.5 immutable objective read model.</summary>
    public readonly struct QuestObjectiveView { public readonly string Id, Text; public readonly bool Done, Optional; public readonly int Progress, Target;
        public QuestObjectiveView(string id, string text, bool done, bool optional, int progress, int target) { Id = id; Text = text; Done = done; Optional = optional; Progress = progress; Target = target; } }
}
