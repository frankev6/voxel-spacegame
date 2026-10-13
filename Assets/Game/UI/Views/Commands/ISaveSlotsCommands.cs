namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface ISaveSlotsCommands
    {
        void Select(int index);
        void LoadSelected();
        void SaveToSelected();
        void SaveNew();
        void DeleteSelected();
    }
}
