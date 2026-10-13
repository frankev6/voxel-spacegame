using Newtonsoft.Json;

namespace Game.Editor.Migration.Narrative
{
    // Strict, source-only shapes. Optional presentation and quest metadata survive for NAR-015/016.
    internal sealed class LegacyEncounterFileDto
    {
        [JsonProperty("encounters", Required = Required.Always)] public LegacyEncounterDto[] Encounters { get; set; }
    }

    internal sealed class LegacyEncounterDto
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("isUnique")] public bool IsUnique { get; set; }
        [JsonProperty("repeatable")] public bool Repeatable { get; set; }
        [JsonProperty("disableInputsMode")] public bool DisableInputsMode { get; set; }
        [JsonProperty("lootTable")] public string LootTable { get; set; }
        [JsonProperty("pages", Required = Required.Always)] public LegacyPageDto[] Pages { get; set; }
        [JsonIgnore] public string SourcePath { get; set; }
    }

    internal sealed class LegacyPageDto
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("text", Required = Required.Always)] public string Text { get; set; }
        [JsonProperty("image")] public string Image { get; set; }
        [JsonProperty("audio")] public string Audio { get; set; }
        [JsonProperty("tags", Required = Required.DisallowNull)] public string[] Tags { get; set; } = System.Array.Empty<string>();
        [JsonProperty("options", Required = Required.Always)] public LegacyOptionDto[] Options { get; set; }
    }

    internal sealed class LegacyOptionDto
    {
        [JsonProperty("text", Required = Required.Always)] public string Text { get; set; }
        [JsonProperty("showReq")] public string ShowReq { get; set; }
        [JsonProperty("enableReq")] public string EnableReq { get; set; }
        [JsonProperty("actions", Required = Required.Always)] public string Actions { get; set; }
        [JsonProperty("consume")] public bool Consume { get; set; }
        [JsonProperty("style")] public string Style { get; set; }
    }

    internal sealed class LegacyQuestFileDto
    {
        [JsonProperty("quests", Required = Required.Always)] public LegacyQuestDto[] Quests { get; set; }
    }

    internal sealed class LegacyQuestDto
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("shortDescription")] public string ShortDescription { get; set; }
        [JsonProperty("detailedDescription")] public string DetailedDescription { get; set; }
        [JsonProperty("isUnique")] public bool IsUnique { get; set; }
        [JsonProperty("repeatable")] public bool Repeatable { get; set; }
        [JsonProperty("autoTrack")] public bool AutoTrack { get; set; }
        [JsonProperty("isStory")] public bool IsStory { get; set; }
        [JsonProperty("objectives", Required = Required.DisallowNull)] public LegacyObjectiveDto[] Objectives { get; set; } = System.Array.Empty<LegacyObjectiveDto>();
        [JsonProperty("entityOptions", Required = Required.DisallowNull)] public LegacyEntityOptionDto[] EntityOptions { get; set; } = System.Array.Empty<LegacyEntityOptionDto>();
        [JsonProperty("markers", Required = Required.DisallowNull)] public LegacyMarkerDto[] Markers { get; set; } = System.Array.Empty<LegacyMarkerDto>();
        [JsonProperty("onAcceptedActions", Required = Required.DisallowNull)] public LegacyQuestActionDto[] OnAcceptedActions { get; set; } = System.Array.Empty<LegacyQuestActionDto>();
        [JsonProperty("onCompletedActions", Required = Required.DisallowNull)] public LegacyQuestActionDto[] OnCompletedActions { get; set; } = System.Array.Empty<LegacyQuestActionDto>();
        [JsonProperty("onFailedActions", Required = Required.DisallowNull)] public LegacyQuestActionDto[] OnFailedActions { get; set; } = System.Array.Empty<LegacyQuestActionDto>();
        [JsonIgnore] public string SourcePath { get; set; }
    }

    internal sealed class LegacyObjectiveDto
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("objectiveType")] public string ObjectiveType { get; set; }
        [JsonProperty("targetId")] public string TargetId { get; set; }
        [JsonProperty("targetCount")] public int TargetCount { get; set; }
        [JsonProperty("condition")] public LegacyConditionDto Condition { get; set; }
    }

    internal sealed class LegacyConditionDto
    {
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("value")] public string Value { get; set; }
        [JsonProperty("questId")] public string QuestId { get; set; }
        [JsonProperty("objectiveId")] public string ObjectiveId { get; set; }
        [JsonProperty("comparison")] public string Comparison { get; set; }
        [JsonProperty("intValue")] public int IntValue { get; set; }
        [JsonProperty("all", Required = Required.DisallowNull)] public LegacyConditionDto[] All { get; set; }
    }

    internal sealed class LegacyEntityOptionDto
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("entityId")] public string EntityId { get; set; }
        [JsonProperty("scope")] public string Scope { get; set; }
        [JsonProperty("text", Required = Required.Always)] public string Text { get; set; }
        [JsonProperty("consumeOnUse")] public bool ConsumeOnUse { get; set; }
        [JsonProperty("showCondition")] public LegacyConditionDto ShowCondition { get; set; }
        [JsonProperty("enableCondition")] public LegacyConditionDto EnableCondition { get; set; }
        [JsonProperty("outcomes", Required = Required.DisallowNull)] public LegacyOutcomeDto[] Outcomes { get; set; } = System.Array.Empty<LegacyOutcomeDto>();
    }

    internal sealed class LegacyOutcomeDto
    {
        [JsonProperty("type", Required = Required.Always)] public string Type { get; set; }
        [JsonProperty("parameter1")] public string Parameter1 { get; set; }
        [JsonProperty("parameter2")] public string Parameter2 { get; set; }
    }

    internal sealed class LegacyMarkerDto
    {
        [JsonProperty("entityId")] public string EntityId { get; set; }
        [JsonProperty("systemId")] public string SystemId { get; set; }
        [JsonProperty("label")] public string Label { get; set; }
    }

    internal sealed class LegacyQuestActionDto
    {
        [JsonProperty("actionType", Required = Required.Always)] public string ActionType { get; set; }
        [JsonProperty("questId")] public string QuestId { get; set; }
        [JsonProperty("templateId")] public string TemplateId { get; set; }
        [JsonProperty("objectiveId")] public string ObjectiveId { get; set; }
        [JsonProperty("encounterId")] public string EncounterId { get; set; }
        [JsonProperty("entityId")] public string EntityId { get; set; }
        [JsonProperty("logText")] public string LogText { get; set; }
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("value")] public string Value { get; set; }
        [JsonProperty("boolValue")] public bool BoolValue { get; set; }
        [JsonProperty("intValue")] public int IntValue { get; set; }
        [JsonProperty("parameters", Required = Required.DisallowNull)] public LegacyParameterDto[] Parameters { get; set; } = System.Array.Empty<LegacyParameterDto>();
    }

    internal sealed class LegacyParameterDto
    {
        [JsonProperty("key", Required = Required.Always)] public string Key { get; set; }
        [JsonProperty("value", Required = Required.Always)] public string Value { get; set; }
    }

    internal sealed class LegacyTriggerFileDto
    {
        [JsonProperty("triggers", Required = Required.Always)] public LegacyTriggerDto[] Triggers { get; set; }
    }

    internal sealed class LegacyTriggerDto
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("eventType", Required = Required.Always)] public string EventType { get; set; }
        [JsonProperty("oncePerRun")] public bool OncePerRun { get; set; }
        [JsonProperty("condition")] public LegacyConditionDto Condition { get; set; }
        [JsonProperty("actions", Required = Required.Always)] public LegacyQuestActionDto[] Actions { get; set; }
        [JsonIgnore] public string SourcePath { get; set; }
    }

    internal sealed class LegacyTemplateFileDto
    {
        [JsonProperty("templates", Required = Required.Always)] public LegacyTemplateDto[] Templates { get; set; }
    }

    internal sealed class LegacyTemplateDto
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("idPrefix")] public string IdPrefix { get; set; }
        [JsonProperty("titleFormat")] public string TitleFormat { get; set; }
        [JsonProperty("shortDescriptionFormat")] public string ShortDescriptionFormat { get; set; }
        [JsonProperty("detailedDescriptionFormat")] public string DetailedDescriptionFormat { get; set; }
        [JsonProperty("autoTrack")] public bool AutoTrack { get; set; }
        [JsonProperty("objectives", Required = Required.DisallowNull)] public LegacyObjectiveDto[] Objectives { get; set; } = System.Array.Empty<LegacyObjectiveDto>();
        [JsonProperty("markers", Required = Required.DisallowNull)] public LegacyMarkerDto[] Markers { get; set; } = System.Array.Empty<LegacyMarkerDto>();
        [JsonProperty("onAcceptedActions", Required = Required.DisallowNull)] public LegacyQuestActionDto[] OnAcceptedActions { get; set; } = System.Array.Empty<LegacyQuestActionDto>();
        [JsonProperty("onCompletedActions", Required = Required.DisallowNull)] public LegacyQuestActionDto[] OnCompletedActions { get; set; } = System.Array.Empty<LegacyQuestActionDto>();
        [JsonIgnore] public string SourcePath { get; set; }
    }

}
