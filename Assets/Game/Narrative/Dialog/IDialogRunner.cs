namespace Game.Narrative.Dialog
{
    /// <summary>The only dialog engine API. Implemented by YarnDialogRunner or GraphDialogRunner. Implements SPEC-41 §4.6.</summary>
    public interface IDialogRunner
    {
        bool HasNode(string node);
        bool IsRunning { get; }
        void Start(string node, IDialogRunnerHost host);   // runs until the first line, option set, blocking command or end
        void Continue();                                   // after OnLine
        void SelectOption(int optionId);                   // after OnOptions
        void Resume();                                     // after OnCommand returned Block
        void Stop();                                       // immediate; host receives OnDialogComplete
    }
}
