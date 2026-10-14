using System;
using Game.Core;
using Game.Input;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>Real authored screen, navigation and input; no production runner substitute.</summary>
    internal sealed class DialogViewFixture : IDisposable
    {
        internal sealed class Commands : IDialogCommands
        {
            internal int Number, Choices, HostChoices, Index = -1, Clicks, Continues, Backs;
            public void Choose(int number) { HostChoices++; ChooseByNumber(number); }
            public void ChooseByNumber(int number) { Number = number; Choices++; }
            public void ChooseOption(int index) { Index = index; Clicks++; }
            public void Continue() => Continues++;
            public bool Back() { Backs++; return true; }
        }
        internal readonly UITestPanel Panel;
        internal readonly InputService Input = new(null);
        internal readonly InputServiceBridge Bridge;
        internal readonly DialogTestContext.Pause Pause = new();
        internal readonly DialogViewModel Model = new();
        internal readonly Commands Actions = new();
        internal readonly DialogView View;
        internal readonly UIRoot UI;
        readonly UIConfig config;
        readonly InputService previous;
        Texture2D texture;
        Sprite sprite;
        internal DialogViewFixture(int width = 1280, int height = 720)
        {
            Services.TryGet(out previous); if (previous != null) Services.Unregister(previous);
            Services.Register(Input);
            Bridge = new InputServiceBridge(Input); Panel = new UITestPanel(width, height);
            config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
            config.PanelSettings = Panel.Settings;
            UI = new UIRoot(Panel.Document, config, Pause, Bridge, new NullUISoundSink(), new UnityFrameClock());
            Bridge.BindBack(UI.HandleBackPressed);
            UI.Register(new DialogTestContext.RootView()); UI.Navigator.SetRoot(UI.Navigator.Get<DialogTestContext.RootView>());
            View = new DialogView(Model, Actions); UI.Register(View);
        }
        internal void Show() => UI.Navigator.Push(View);
        internal void Populate(int lineCount = 6, int optionCount = 3)
        {
            Model.Lines.Clear(); Model.Options.Clear();
            for (int i = 0; i < lineCount; i++) Model.Lines.Add(new DialogLineViewModel { SpeakerName = "NORA", Text = "The signal repeats beneath the static. Somewhere beyond the gate, another station is listening. The crew waits for your decision." });
            for (int i = 0; i < optionCount; i++) Model.Options.Add(new DialogOptionViewModel { Text = "Follow the signal and inspect the station", NumberLabel = i < 9 ? (i + 1).ToString() : "", IsAvailable = true, IsQuest = i == 0 });
            Model.NotifyLinesChanged(); Model.NotifyOptionsChanged();
        }
        internal void AddImages()
        {
            texture = new Texture2D(2, 2); sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            Model.Portrait = Model.Illustration = sprite; Model.HasPortrait = Model.HasIllustration = true;
        }
        public void Dispose()
        {
            UI.Dispose(); Bridge.Dispose(); Services.Unregister(Input); Input.Dispose();
            if (previous != null) Services.Register(previous);
            if (sprite != null) Object.Destroy(sprite); if (texture != null) Object.Destroy(texture);
            Object.Destroy(config); Panel.Dispose();
        }
    }
}
