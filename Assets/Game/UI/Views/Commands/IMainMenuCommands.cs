namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IMainMenuCommands
    {
        void Continue();
        void NewGame();
        void Load();
        void OpenLab();
        void OpenSettings();
        void Quit();
    }
}
