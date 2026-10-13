namespace Game.Narrative.Dialog
{
    /// <summary>Implements SPEC-41 §4.6.</summary>
    public interface IDialogRunnerHost
    {
        void OnLine(in DialogLine line);                         // runner waits for Continue()
        void OnOptions(DialogOption[] options, int count);       // runner waits for SelectOption()
        DialogCommandResult OnCommand(string commandText);       // exact normalized text of <<...>> (not jump/detour/return/stop/set/if)
        void OnNodeStarted(string node);
        void OnNodeCompleted(string node);
        void OnDialogComplete();                                 // after stop, end of the top node, or Stop()
    }
}
