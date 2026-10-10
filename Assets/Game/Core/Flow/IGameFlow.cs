namespace Game.Core.Flow
{
    /// <summary>Game-level flows for modules below Game.App (ShipEditor, Narrative adapters, tools). Implemented by SPEC-01 GameSession. Methods never throw.
    /// Slot IDs are SPEC-05 folder names inside the active profile: "manual_001", "quick", "auto_0". Implements SPEC-02 §4.8.</summary>
    public interface IGameFlow
    {
        GameScene CurrentScene { get; }
        bool IsTransitioning { get; }
        bool HasActiveSession { get; }
        bool IsCreativeSession { get; }
        Game.Core.Save.SaveBlockReason GetSaveBlock();
        UnityEngine.Awaitable<FlowResult> NewGameAsync(NewGameArgs args);
        UnityEngine.Awaitable<FlowResult> LoadGameAsync(string slotId);
        UnityEngine.Awaitable<FlowResult> SaveGameAsync(string slotId, Game.Core.Save.SaveReason reason);
        UnityEngine.Awaitable<FlowResult> QuitToMainMenuAsync(bool saveFirst);
        UnityEngine.Awaitable<FlowResult> EnterBlueprintLabAsync(BlueprintLabArgs args);
        UnityEngine.Awaitable<FlowResult> ExitBlueprintLabAsync();
        void QuitToDesktop();
    }
}
