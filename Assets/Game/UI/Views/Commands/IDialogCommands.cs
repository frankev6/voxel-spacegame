namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface IDialogCommands
    {
        void Choose(int number);
        void Continue();
    }
}
