using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.App.Flow;
using Game.App.Presenters;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Input;
using Game.Persistence.Services;
using Game.Tests.Common;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI.Screens
{
    /// <summary>HUD-006: real boot/session, keyboard Back, shared pause owner and confirmations; four required cases.</summary>
    public sealed class PauseViewTests : GameFlowFixture
    {
        sealed class KeyboardInput : IDisposable
        {
            readonly Keyboard keyboard = InputSystem.AddDevice<Keyboard>("HUD006Keyboard");
            internal void Escape()
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            }
            public void Dispose() { if (keyboard.added) InputSystem.RemoveDevice(keyboard); }
        }
        // Logical-size layout is isolated from the Editor's game-view size, while the
        // presenter still reads the production IGameFlow and the real shared pause owner.
        sealed class LayoutWindow : IDisposable
        {
            internal readonly UITestPanel Panel = new(1280, 720);
            internal readonly UIRoot UI;
            internal readonly PauseViewModel Model = new();
            internal readonly PauseView View;
            readonly PausePresenter presenter;
            readonly UIConfig config;
            internal LayoutWindow(IGameFlow flow, IPauseService pause)
            {
                config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
                config.PanelSettings = Panel.Settings;
                UI = new UIRoot(Panel.Document, config, pause, new DialogTestContext.Context(), new NullUISoundSink(), new UnityFrameClock());
                var root = new DialogTestContext.RootView(); UI.Register(root); UI.Navigator.SetRoot(root);
                presenter = new PausePresenter(UI, Model, flow,
                    _ => throw new InvalidOperationException("This layout case does not navigate to slots."),
                    () => throw new InvalidOperationException("This layout case does not navigate to settings."));
                View = new PauseView(Model, presenter); UI.Register(View); presenter.Bind();
                UI.ApplyAccessibility(new AccessibilityOptions { UiScale = 1, TextScale = TextScale.Percent130 });
                UI.Navigator.Push(View);
            }
            public void Dispose() { presenter.Dispose(); UI.Dispose(); Object.Destroy(config); Panel.Dispose(); }
        }
        [UnityTest] public IEnumerator EscTwice_ResumesAndRestoresFlight()
        {
            var operation = Service<SceneFlow>().EnterSandboxAsync(new SandboxArgs(false, 42));
            var awaiter = operation.GetAwaiter();
            yield return WaitUntil(() => awaiter.IsCompleted, 15, "Playerless sandbox enters World");
            Assert.AreEqual(FlowResult.Ok, awaiter.GetResult());
            var input = Service<InputService>(); var ui = Service<UIRoot>(); var pause = Service<IPauseService>();
            yield return WaitUntil(() => input.CurrentContext == InputContext.Flight, 5, "World root owns Flight input");
            Assert.AreEqual(0, pause.ActiveTokenCount);
            using var keyboard = new KeyboardInput(); keyboard.Escape();
            Assert.IsTrue(ui.Navigator.Top is PauseView); Assert.AreEqual(InputContext.Menu, input.CurrentContext);
            Assert.AreEqual(1, pause.ActiveTokenCount);
            var view = ui.Navigator.Get<PauseView>();
            Assert.AreEqual(input.GetBindingDisplay("Global/Back"), view.Root.Q<KeyHint>("pause-back-key").text);
            Assert.AreEqual(input.GetBindingDisplay("Global/DevConsole"), view.Root.Q<KeyHint>("pause-console-key").text);
            keyboard.Escape();
            Assert.IsFalse(view.IsOpen); Assert.AreEqual(InputContext.Flight, input.CurrentContext); Assert.AreEqual(0, pause.ActiveTokenCount);
        }
        [UnityTest] public IEnumerator SaveBlocked_ShowsReason()
        {
            var flow = Service<IGameFlow>(); var ui = Service<UIRoot>();
            Assert.AreEqual(SaveBlockReason.NoSession, flow.GetSaveBlock());
            var view = ui.Navigator.Get<PauseView>(); ui.Navigator.Push(view);
            yield return null;
            Assert.IsFalse(view.Root.Q<Button>("pause-save").enabledSelf);
            var reason = view.Root.Q<Label>("pause-save-blocked");
            Assert.AreEqual("No active game to save.", reason.text); Assert.AreNotEqual(DisplayStyle.None, reason.resolvedStyle.display);
            Assert.AreEqual(reason.text, view.Root.Q<Button>("pause-save").tooltip);
            DialogTestContext.Activate(view.Root.Q<Button>("pause-save"));
            Assert.IsFalse(ui.Navigator.IsOpen<SaveSlotsView>()); Assert.IsFalse(flow.HasActiveSession);
            ui.Navigator.Pop(view);
        }
        [UnityTest] public IEnumerator MainMenu_RequiresConfirm()
        {
            yield return NewGame(7);
            var flow = Service<IGameFlow>(); var ui = Service<UIRoot>(); var save = Service<SaveService>();
            var pause = ui.Navigator.Get<PauseView>(); ui.Navigator.Push(pause);
            DialogTestContext.Activate(pause.Root.Q<Button>("pause-main-menu"));
            Assert.IsTrue(ui.Confirm.IsOpen); Assert.AreEqual(GameScene.World, flow.CurrentScene); Assert.IsTrue(flow.HasActiveSession);
            ui.HandleBackPressed(); Assert.IsFalse(ui.Confirm.IsOpen); Assert.IsTrue(pause.IsOpen); Assert.IsTrue(flow.HasActiveSession);
            int before = save.ListSlots(save.ActiveProfile).Count;
            DialogTestContext.Activate(pause.Root.Q<Button>("pause-main-menu"));
            var confirm = ui.Navigator.Get<ConfirmDialogView>();
            DialogTestContext.Activate(confirm.Root.Q<Button>("confirm-option-0"));
            yield return WaitUntil(() => !flow.IsTransitioning && flow.CurrentScene == GameScene.MainMenu && !flow.HasActiveSession, 30,
                "Confirmed action saves and returns through the real game flow");
            Assert.Greater(save.ListSlots(save.ActiveProfile).Count, before, "Quit confirmation must use saveFirst:true.");
            Assert.IsFalse(pause.IsOpen);
            Assert.AreEqual(0, Service<IPauseService>().ActiveTokenCount);
        }
        [UnityTest] public IEnumerator Layout_TextScale130_1280x720_NoIssues()
        {
            using var window = new LayoutWindow(Service<IGameFlow>(), Service<IPauseService>());
            yield return null;
            var scroll = window.View.Root.Q<ScrollView>("pause-actions-scroll");
            var quit = window.View.Root.Q<Button>("pause-quit"); scroll.ScrollTo(quit); yield return null;
            Assert.IsTrue(scroll.contentViewport.worldBound.Contains(quit.worldBound.center), "The last action stays reachable at 130% text scale.");
            var issues = new List<LayoutIssue>(); UILayoutAudit.Run(window.View.Root, window.Panel.Viewport, issues);
            Assert.IsEmpty(issues, string.Join(", ", issues.Select(issue => issue.Kind + " " + issue.ElementPath)));
            Assert.Greater(window.View.Root.Q<KeyHint>("pause-back-key").resolvedStyle.fontSize, 0);
            Assert.IsFalse(window.View.Root.Q<Button>("pause-save").enabledSelf);
        }
    }
}
