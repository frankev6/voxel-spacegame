namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public readonly struct LogicScope
    {
        public readonly LogicUsage Usage;
        public readonly int QuestIndex;          // IndexOf<QuestDefinition>, 0 = none
        public readonly int QuestInstance;       // 0-based instance for repeatable quests
        public readonly int BeatIndex;           // IndexOf<StoryBeatDefinition>, 0 = none
        public readonly bool HasBody;
        public readonly LogicBodyHandle Body;
        public LogicScope(LogicUsage usage, int questIndex, int questInstance, int beatIndex, bool hasBody, LogicBodyHandle body)
        { Usage = usage; QuestIndex = questIndex; QuestInstance = questInstance; BeatIndex = beatIndex; HasBody = hasBody; Body = body; }
    }
}
