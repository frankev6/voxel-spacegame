namespace Game.Core.Save
{
    /// <summary>Why saving is not possible right now (values defined with SPEC-05 §4.4; append only). Implements SPEC-02 §4.7.</summary>
    public enum SaveBlockReason : byte { None = 0, NoSession = 1, InCombat = 2, Transitioning = 3, PlayerDead = 4, EditorOpen = 5 }
}
