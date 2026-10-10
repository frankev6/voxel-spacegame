namespace Game.Core.Flow
{
    /// <summary>Why a scene is being entered (SPEC-02 §4.8).</summary>
    public enum SceneEntryReason : byte { Startup = 0, NewGame = 1, LoadGame = 2, EnterBlueprintLab = 3, ExitBlueprintLab = 4, QuitToMenu = 5, DevQuickStart = 6, ErrorRecovery = 7 }
}
