using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.Views;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>SPEC-60 UI-005: real runtime panel, controlled frame clock, and non-pausing test views.</summary>
    internal sealed class UIInputTestContext : IDisposable
    {
        internal sealed class Clock : IFrameClock
        {
            public int FrameCount { get; set; } = 20;
            public float UnscaledTime => 0;
        }
        internal sealed class Context : IInputContextTarget
        {
            internal InputContext Current;
            public void SetContext(InputContext value) => Current = value;
        }
        sealed class Pause : IPauseService
        {
            public bool IsPaused => false;
            public int ActiveTokenCount => 0;
            public PauseToken Acquire(string reason) => throw new InvalidOperationException("Test views do not pause");
            public void Release(PauseToken token) { }
            public void GetActiveReasons(List<string> results) => results.Clear();
        }
        internal sealed class RootView : UIScreen
        {
            public override string Key => "test-root";
            public override UILayer Layer => UILayer.Hud;
            public override InputContext Context => InputContext.Flight;
            public override bool IsRoot => true;
            public override bool PausesSimulation => false;
            public override bool BlocksWorldPointer => false;
            protected override void OnBuild() { }
        }
        internal sealed class OverlayView : UIScreen
        {
            public override string Key => "test-overlay";
            public override UILayer Layer => UILayer.Modals;
            public override bool PausesSimulation => false;
            public override bool BlocksWorldPointer => BlockPointer;
            internal bool BlockPointer;
            internal int BackCount;
            protected override void OnBuild() { }
            public override bool OnBack() { BackCount++; return false; }
        }
        readonly GameObject host;
        readonly UIConfig config;
        readonly PanelSettings panel;
        readonly RenderTexture texture;
        internal readonly Clock Frames = new Clock();
        internal readonly Context Target = new Context();
        internal readonly RootView Root = new RootView();
        internal readonly OverlayView Overlay = new OverlayView();
        internal readonly UIRoot UI;
        internal UIInputTestContext()
        {
            config = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
            var empty = config.Screens[0].Uxml;
            config.Screens = new[] { new ScreenAsset { Key = Root.Key, Uxml = empty }, new ScreenAsset { Key = Overlay.Key, Uxml = empty } };
            panel = Object.Instantiate(config.PanelSettings); config.PanelSettings = panel;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
            texture = new RenderTexture(1280, 720, 0); panel.targetTexture = texture;
            // RenderTexture panels use an explicit screen mapping; one input pixel = one panel pixel here.
            panel.SetScreenToPanelSpaceFunction(position => position);
            host = new GameObject("UI input test"); var document = host.AddComponent<UIDocument>();
            UI = new UIRoot(document, config, new Pause(), Target, new NullUISoundSink(), Frames);
            UI.Register(Root); UI.Register(Overlay); UI.Navigator.SetRoot(Root); Frames.FrameCount += 2;
        }
        internal TextField OpenField()
        {
            UI.Navigator.Push(Overlay);
            var field = new TextField { name = "test-input" }; Overlay.Root.Add(field);
            return field;
        }
        internal static Vector2 ToScreen(Vector2 panelPoint) => new Vector2(panelPoint.x, 720 - panelPoint.y);
        public void Dispose()
        {
            UI.Dispose(); Object.Destroy(host); Object.Destroy(config); Object.Destroy(panel); Object.Destroy(texture);
        }
    }
}
