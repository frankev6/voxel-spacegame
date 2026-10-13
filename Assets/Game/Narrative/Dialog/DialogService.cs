namespace Game.Narrative.Dialog
{
    /// <summary>Executes one dialog at a time. Implements SPEC-41 §5.3. Main thread.</summary>
    public sealed class DialogService : IDialogRunnerHost
    {
        public const int MaxLinesPerPump = 16;
        public DialogService(IDialogRunner runner, DialogCommandTable commands, Game.Data.Logic.ILogicContext context, DialogMemory memory) => throw new System.NotImplementedException("DLG-006");
        public bool IsActive { get; }
        public int CurrentBeatIndex { get; }
        public bool TryStart(in DialogRequest request) => throw new System.NotImplementedException("DLG-006");        // false if active or node unknown (error logged)
        public void Pump() => throw new System.NotImplementedException("DLG-006");                                     // once per frame by DialogPresenter (§5.3)
        public void SelectOption(int optionId) => throw new System.NotImplementedException("DLG-006");
        public void AcknowledgeEnd() => throw new System.NotImplementedException("DLG-006");                           // presenter: "Continue" after the last page
        public void AbortByPlayer() => throw new System.NotImplementedException("DLG-006");                            // synthetic "End conversation" → outcome Aborted
        public void Interrupt() => throw new System.NotImplementedException("DLG-006");                                // session reset/load → outcome Interrupted
        public int Roll(int sides) => throw new System.NotImplementedException("DLG-006");                             // §5.6
        public DialogMemory Memory { get; }

        public event System.Action<DialogRequest> Started
        {
            add => throw new System.NotImplementedException("DLG-006");
            remove => throw new System.NotImplementedException("DLG-006");
        }
        public event System.Action<DialogLine> LinePresented
        {
            add => throw new System.NotImplementedException("DLG-006");
            remove => throw new System.NotImplementedException("DLG-006");
        }
        public event System.Action<DialogOption[], int> OptionsPresented
        {
            add => throw new System.NotImplementedException("DLG-006");
            remove => throw new System.NotImplementedException("DLG-006");
        }   // array reused; copy if kept
        public event System.Action PageCleared
        {
            add => throw new System.NotImplementedException("DLG-006");
            remove => throw new System.NotImplementedException("DLG-006");
        }
        public event System.Action WaitingForEndAcknowledge
        {
            add => throw new System.NotImplementedException("DLG-006");
            remove => throw new System.NotImplementedException("DLG-006");
        }    // last page shown, dialog finished
        public event System.Action<DialogEnded> Ended
        {
            add => throw new System.NotImplementedException("DLG-006");
            remove => throw new System.NotImplementedException("DLG-006");
        }
        // TODO(DLG-006): implement SPEC-41 service, command lookup and visit memory behavior.

        public void OnLine(in DialogLine line) => throw new System.NotImplementedException("DLG-006");
        public void OnOptions(DialogOption[] options, int count) => throw new System.NotImplementedException("DLG-006");
        public DialogCommandResult OnCommand(string commandText) => throw new System.NotImplementedException("DLG-006");
        public void OnNodeStarted(string node) => throw new System.NotImplementedException("DLG-006");
        public void OnNodeCompleted(string node) => throw new System.NotImplementedException("DLG-006");
        public void OnDialogComplete() => throw new System.NotImplementedException("DLG-006");
    }
}
