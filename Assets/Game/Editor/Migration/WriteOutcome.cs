namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2.</summary>
    public enum WriteOutcome : byte { Created = 0, Updated = 1, Unchanged = 2, SkippedHandEdited = 3, Failed = 4 }
}
