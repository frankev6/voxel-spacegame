namespace Game.UI.Views
{
    /// <summary>Dialog presenter commands (SPEC-41 §4.9), retaining the staged SPEC-61 §4.3 Choose signature.</summary>
    public interface IDialogCommands
    {
        void Choose(int number);
        void ChooseByNumber(int number);
        void ChooseOption(int listIndex);
        void Continue();
        bool Back();
    }
}
