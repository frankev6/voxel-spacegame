#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Core;
using Game.UI.Data;
using Game.UI.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.App.DevRuntime.Console
{
    /// <summary>Cached, code-built development view using only shared theme classes (ADR-0049).</summary>
    internal sealed class DevConsoleScreen : UIScreen
    {
        readonly DevConsole console;
        ListView output;
        internal TextField Input { get; private set; }
        internal DevConsoleScreen(DevConsole console) { this.console = console; }
        public override string Key => "dev-console";
        public override UILayer Layer => UILayer.Modals;
        public override InputContext Context => InputContext.TextEntry;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => true;
        public override VisualElement DefaultFocus => Input;
        public override VisualElement CreateView() => new VisualElement();
        protected override void OnBuild()
        {
            var panel = new VisualElement { name = "panel-dev-console" }; panel.AddToClassList("panel"); panel.AddToClassList("grow"); Root.Add(panel);
            var header = new VisualElement { name = "panel-dev-console-header" }; header.AddToClassList("panel__header"); panel.Add(header);
            var title = new Label("Developer console") { name = "lbl-dev-console-title" }; title.AddToClassList("panel__title"); header.Add(title);
            var close = new Button(console.Toggle) { text = "Close", name = "btn-dev-console-close" }; close.AddToClassList("btn"); close.AddToClassList("btn--secondary"); header.Add(close);
            var hint = new Label("F1 close · ↑/↓ history · Tab complete") { name = "lbl-dev-console-hint" }; hint.AddToClassList("text-muted"); header.Add(hint);
            var body = new VisualElement { name = "panel-dev-console-body" }; body.AddToClassList("panel__body"); panel.Add(body);
            output = new ListView { name = "list-dev-console-output", itemsSource = console.Lines, selectionType = SelectionType.None,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight, makeItem = MakeLine, bindItem = BindLine };
            output.AddToClassList("list"); body.Add(output);
            Input = new TextField { name = "input-dev-command", label = "Command", multiline = false };
            Input.AddToClassList("text-field"); Input.AddToClassList("text-mono"); body.Add(Input);
            Input.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
        }
        VisualElement MakeLine()
        {
            var label = new Label { name = "lbl-dev-console-line" }; label.AddToClassList("list__item-label"); label.AddToClassList("text-mono");
            return label;
        }
        void BindLine(VisualElement element, int index) => ((Label)element).text = console.Lines[index];
        void OnKey(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Return: case KeyCode.KeypadEnter:
                    string command = Input.value; Input.SetValueWithoutNotify(string.Empty); console.Execute(command); break;
                case KeyCode.UpArrow: SetInput(console.Recall(-1, Input.value)); break;
                case KeyCode.DownArrow: SetInput(console.Recall(1, Input.value)); break;
                case KeyCode.Tab: SetInput(console.Complete(Input.value)); break;
                default: return;
            }
            evt.StopImmediatePropagation();
        }
        void SetInput(string value) { Input.SetValueWithoutNotify(value); Input.SelectRange(value.Length, value.Length); }
        internal void Refresh()
        {
            if (output == null) return;
            output.RefreshItems();
            if (console.Lines.Count > 0) output.ScrollToItem(console.Lines.Count - 1);
        }
        public override void OnPush(object args) { Refresh(); }
        public override void OnPop() { Input?.Blur(); }
    }
}
#endif
