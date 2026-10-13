namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IShipLogCommands
    {
        void SelectTab(int tab);
        void SetShowCompleted(bool on);
        void SelectQuest(int index);
        void ToggleTrack(int index);
        void SelectClue(int index);
        void ShowRumorOnMap(int index);
    }
}
