namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.3 authored quest log text keyed by entry ID.</summary>
    [System.Serializable] public struct QuestLogEntry { public string id; [UnityEngine.TextArea(1, 4)] public string text; }
}
