using System;
using Game.Narrative.Dialog;

namespace Game.Tests.EditMode.Narrative
{
    internal sealed class FakeDialogRunner : IDialogRunner
    {
        readonly Action<IDialogRunnerHost>[] steps;
        IDialogRunnerHost host;
        int cursor, depth;
        public int MaxDepth, Continues, Resumes, Selections;
        public bool IsRunning { get; private set; }
        public FakeDialogRunner(params Action<IDialogRunnerHost>[] steps) => this.steps = steps;
        public bool HasNode(string node) => node == "start";
        public void Start(string node, IDialogRunnerHost host)
        { this.host = host; cursor = 0; IsRunning = true; host.OnNodeStarted(node); Advance(); }
        public void Continue() { Continues++; Advance(); }
        public void Resume() { Resumes++; Advance(); }
        public void SelectOption(int optionId) { Selections++; Advance(); }
        public void Stop() { if (!IsRunning) return; IsRunning = false; host.OnDialogComplete(); }
        void Advance()
        {
            depth++; MaxDepth = Math.Max(depth, MaxDepth);
            try { if (cursor < steps.Length) steps[cursor++](host); else Stop(); }
            finally { depth--; }
        }
        public static Action<IDialogRunnerHost> Line(string text) => host =>
            host.OnLine(new DialogLine(text, text, "", "", "", "", "start", false));
        public Action<IDialogRunnerHost> Command(string text) => host =>
        {
            var result = host.OnCommand(text);
            if (result == DialogCommandResult.Stop) Stop();
            else if (result == DialogCommandResult.Continue) Advance();
        };
    }
}
