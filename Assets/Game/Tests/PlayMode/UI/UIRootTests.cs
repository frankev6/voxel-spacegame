using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>SPEC-60 UI-003: real panel lifecycle and bubbling sound events on dynamically added buttons.</summary>
    public sealed class UIRootTests
    {
        sealed class Sound : IUISoundSink
        {
            public readonly List<UISound> Heard = new List<UISound>();
            public void Play(UISound sound) => Heard.Add(sound);
        }
        sealed class Context : IInputContextTarget { public void SetContext(InputContext context) { } }
        sealed class Pause : IPauseService
        {
            public bool IsPaused => false;
            public int ActiveTokenCount => 0;
            public PauseToken Acquire(string reason) => throw new InvalidOperationException("UI-003 must not acquire a navigation token");
            public void Release(PauseToken token) { }
            public void GetActiveReasons(List<string> results) => results.Clear();
        }
        sealed class PrewarmView : UIScreen
        {
            public override string Key => "prewarm";
            public override UILayer Layer => UILayer.Screens;
            public int Builds;
            protected override void OnBuild() => Builds++;
        }
        GameObject host;
        UIDocument document;
        UIConfig config;
        PanelSettings panel;
        RenderTexture texture;
        UIRoot ui;
        Sound sound;
        [UnitySetUp] public IEnumerator SetUp()
        {
            config = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
            panel = Object.Instantiate(config.PanelSettings); config.PanelSettings = panel;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            texture = new RenderTexture(1280, 720, 0); panel.targetTexture = texture;
            host = new GameObject("UIRoot lifecycle test"); document = host.AddComponent<UIDocument>();
            sound = new Sound(); yield return null;
        }
        UIRoot Construct() => new UIRoot(document, config, new Pause(), new Context(), sound, new UnityFrameClock());
        [UnityTearDown] public IEnumerator TearDown()
        {
            ui?.Dispose(); ui = null;
            Object.Destroy(host); Object.Destroy(config); Object.Destroy(panel); Object.Destroy(texture);
            yield return null;
        }
        static void Click(Button button)
        { using var evt = ClickEvent.GetPooled(); evt.target = button; button.SendEvent(evt); }
        static void Over(VisualElement element)
        { using var evt = PointerOverEvent.GetPooled(); evt.target = element; element.SendEvent(evt); }
        static void Out(VisualElement element)
        { using var evt = PointerOutEvent.GetPooled(); evt.target = element; element.SendEvent(evt); }
        Button AddButton()
        {
            var button = new Button { name = "dynamic-button", text = "Test" };
            ui.GetLayer(UILayer.Screens).Add(button); return button;
        }
        [UnityTest] public IEnumerator Construct_ValidConfig_SixLayersInOrder()
        {
            config.Screens = new[] { new ScreenAsset { Key = "prewarm", Uxml = config.RootUxml, Prewarm = true } };
            ui = Construct(); yield return null;
            Assert.IsNotNull(ui.Panel);
            Assert.AreEqual("ui-root", ui.RootElement.name);
            for (int i = 0; i < 6; i++) Assert.AreSame(ui.RootElement[i], ui.GetLayer((UILayer)i));
            var view = new PrewarmView(); ui.Register(view);
            Assert.IsTrue(view.IsBuilt); Assert.AreEqual(1, view.Builds);
            Assert.AreSame(view, ui.Navigator.Get<PrewarmView>());
            Assert.IsTrue(view.Root.ClassListContains(UIStyleClasses.IsHidden));
        }
        [UnityTest] public IEnumerator Construct_DuplicateScreenKey_Throws()
        {
            config.Screens = new[] { new ScreenAsset { Key = "duplicate", Uxml = config.RootUxml }, new ScreenAsset { Key = "duplicate", Uxml = config.RootUxml } };
            LogAssert.Expect(LogType.Error, new Regex("\\[UI\\] UIConfig duplicate screen key 'duplicate'"));
            Assert.Throws<InvalidOperationException>(() => Construct());
            yield return null;
        }
        [UnityTest] public IEnumerator DynamicButton_Click_PlaysClickSound()
        {
            ui = Construct(); var button = AddButton(); yield return null;
            sound.Heard.Clear(); Click(button); yield return null;
            CollectionAssert.AreEqual(new[] { UISound.Click }, sound.Heard);
        }
        [UnityTest] public IEnumerator DynamicButton_Hover_PlaysOnce()
        {
            ui = Construct(); var button = AddButton(); var child = new Label("child"); button.Add(child); yield return null;
            Out(button); sound.Heard.Clear(); Over(button); Over(button); Over(child); yield return null;
            CollectionAssert.AreEqual(new[] { UISound.Hover }, sound.Heard);
            Out(button); Over(button); yield return null;
            CollectionAssert.AreEqual(new[] { UISound.Hover, UISound.Hover }, sound.Heard);
        }
        [UnityTest] public IEnumerator Dispose_ThenConstructAgain_Works()
        {
            ui = Construct(); var oldButton = AddButton(); yield return null;
            var first = ui; first.Dispose(); first.Dispose();
            sound.Heard.Clear(); Click(oldButton);
            Assert.AreEqual(0, sound.Heard.Count, "Old tree callbacks removed");
            ui = Construct(); var newButton = AddButton(); yield return null;
            sound.Heard.Clear(); Click(newButton); yield return null;
            CollectionAssert.AreEqual(new[] { UISound.Click }, sound.Heard, "One callback after reconstruction");
        }
    }
}
