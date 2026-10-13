namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IPauseCommands
    {
        void Resume();
        void Save();
        void Load();
        void OpenSettings();
        void MainMenu();
        void Quit();
    }
}
