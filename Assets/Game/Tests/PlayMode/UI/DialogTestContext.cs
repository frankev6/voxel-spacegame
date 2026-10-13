using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>UI-011: real runtime panel with controlled context/token services and condition-based binding/focus waits.</summary>
    internal sealed class DialogTestContext : IDisposable
    {
        internal sealed class Context : IInputContextTarget
        {
            internal InputContext Current;
            public void SetContext(InputContext value) => Current = value;
        }
        internal sealed class Pause : IPauseService
        {
            readonly HashSet<uint> tokens = new HashSet<uint>();
            uint next = 1;
            public bool IsPaused => tokens.Count != 0;
            public int ActiveTokenCount => tokens.Count;
            public PauseToken Acquire(string reason) { uint id = next++; tokens.Add(id); return new PauseToken(this, id); }
            public void Release(PauseToken token) => tokens.Remove(token.Id);
            public void GetActiveReasons(List<string> results) { results.Clear(); if (IsPaused) results.Add("UINavigator"); }
        }
        internal sealed class RootView : UIScreen
        {
            public override string Key => "dialog-test-root";
            public override UILayer Layer => UILayer.Hud;
            public override InputContext Context => InputContext.Flight;
            public override bool IsRoot => true;
            public override bool PausesSimulation => false;
            public override bool BlocksWorldPointer => false;
            public override VisualElement CreateView() => new VisualElement { pickingMode = PickingMode.Ignore };
            protected override void OnBuild() { }
        }
        readonly GameObject host;
        readonly UIConfig config;
        readonly PanelSettings panel;
        readonly RenderTexture texture;
        internal readonly Context Target = new Context();
        internal readonly Pause Tokens = new Pause();
        internal readonly RootView Root = new RootView();
        internal readonly UIRoot UI;
        internal DialogTestContext()
        {
            config = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
            panel = Object.Instantiate(config.PanelSettings); config.PanelSettings = panel;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
            texture = new RenderTexture(1280, 720, 0); panel.targetTexture = texture;
            panel.SetScreenToPanelSpaceFunction(position => position);
            host = new GameObject("UI dialog test"); var document = host.AddComponent<UIDocument>();
            UI = new UIRoot(document, config, Tokens, Target, new NullUISoundSink(), new UnityFrameClock());
            UI.Register(Root); UI.Navigator.SetRoot(Root);
        }
        internal ConfirmDialogView ConfirmView => UI.Navigator.Get<ConfirmDialogView>();
        internal TextPromptView PromptView => UI.Navigator.Get<TextPromptView>();
        internal static ConfirmRequest Confirm(string title = "Confirm", int cancel = 1) => new ConfirmRequest
        {
            Title = title, Body = "Choose an option", CancelIndex = cancel,
            Options = new[] { new ConfirmOption { Label = "Accept", Enabled = true, Variant = ButtonVariant.Primary },
                new ConfirmOption { Label = "Cancel", Enabled = true, Variant = ButtonVariant.Secondary } }
        };
        internal static IEnumerator Until(Func<bool> ready, string description)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!ready())
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, description + " timed out");
                yield return null;
            }
        }
        internal static void Activate(Button button)
        { using var evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt); }
        internal static void Hover(VisualElement element)
        { using var evt = PointerEnterEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = element.worldBound.center }); evt.target = element; element.SendEvent(evt); }
        internal static void Leave(VisualElement element)
        { using var evt = PointerLeaveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = element.worldBound.center }); evt.target = element; element.SendEvent(evt); }
        internal static void Enter(TextField field, KeyCode code = KeyCode.Return)
        { using var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = code }); evt.target = field; field.SendEvent(evt); }
        public void Dispose()
        {
            UI.Dispose(); Assert.AreEqual(0, Tokens.ActiveTokenCount, "Disposal releases the modal pause token");
            Object.Destroy(host); Object.Destroy(config); Object.Destroy(panel); Object.Destroy(texture);
        }
    }
}
