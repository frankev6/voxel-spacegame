using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.App.Presenters;
using Game.App.Session;
using Game.App.Testing;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Galaxy.Services;
using Game.Input;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.Tests.Common;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI.Screens
{
    /// <summary>Exactly HUD-003's five cases. Production boot supplies the real save/flow owners; the window owns only its panel.</summary>
    public sealed class MainMenuViewTests : GameFlowFixture
    {
        sealed class Window : IDisposable
        {
            internal readonly UITestPanel Panel;
            internal readonly UIRoot UI;
            internal readonly MainMenuViewModel Model = new();
            internal readonly MainMenuPresenter Presenter;
            internal readonly MainMenuView View;
            internal int Loads, Labs, Settings;
            readonly InputService input = new(null);
            readonly InputServiceBridge bridge;
            readonly UIConfig config;
            internal Window(IGameFlow flow, SaveService save, IPauseService pause, int width = 1280, int height = 720)
            {
                Panel = new UITestPanel(width, height); bridge = new InputServiceBridge(input);
                config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset")); config.PanelSettings = Panel.Settings;
                UI = new UIRoot(Panel.Document, config, pause, bridge, new NullUISoundSink(), new UnityFrameClock());
                bridge.BindBack(UI.HandleBackPressed);
                // These callbacks verify delegation only; real root navigation and Lab launching remain HUD-002/EDT-023.
                Presenter = new MainMenuPresenter(UI, Model, flow, save, () => Loads++, () => Labs++, () => Settings++);
                View = new MainMenuView(Model, Presenter); UI.Register(View); Presenter.Bind(); UI.Navigator.SetRoot(View);
            }
            internal TextPromptView Prompt => UI.Navigator.Get<TextPromptView>();
            internal TextField Field => Prompt.Root.Q<TextField>("text-prompt-field");
            internal Button Ok => Prompt.Root.Q<Button>("text-prompt-ok");
            internal void Click(string name) => DialogTestContext.Activate(View.Root.Q<Button>(name));
            public void Dispose() { Presenter.Dispose(); UI.Dispose(); bridge.Dispose(); input.Dispose(); Object.Destroy(config); Panel.Dispose(); }
        }
        Window Open(int width = 1280, int height = 720)
            => new(Service<IGameFlow>(), Service<SaveService>(), Service<IPauseService>(), width, height);
        static void Audit(VisualElement root, Rect viewport)
        {
            var issues = new List<LayoutIssue>(); UILayoutAudit.Run(root, viewport, issues);
            Assert.IsEmpty(issues, string.Join("\n", issues.Select(issue => issue.Kind + " " + issue.ElementPath)));
        }
        static IEnumerator Complete<T>(Awaitable<T> operation, Action<T> check)
        {
            var awaiter = operation.GetAwaiter();
            for (int frame = 0; frame < 30 && !awaiter.IsCompleted; frame++) yield return null;
            Assert.IsTrue(awaiter.IsCompleted, "The real owner operation completes within the bounded fixture.");
            check(awaiter.GetResult());
        }
        static IEnumerator Settle(Window window, Func<bool> completed)
        {
            for (int frame = 0; frame < 30; frame++)
            {
                window.Presenter.Tick();
                if (completed()) yield break;
                yield return null;
            }
            window.Presenter.Tick(); Assert.IsTrue(completed(), "The real menu operation completes within the bounded fixture.");
        }

        [UnityTest] public IEnumerator NoSave_ContinueHidden()
        {
            using var window = Open(); yield return null;
            Assert.IsEmpty(Service<SaveService>().ListSlots(Service<SaveService>().ActiveProfile));
            Assert.IsFalse(window.Model.HasContinue);
            Assert.AreEqual(DisplayStyle.None, window.View.Root.Q<Button>("main-menu-continue").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, window.View.Root.Q<Label>("main-menu-continue-subtitle").resolvedStyle.display);
            Assert.AreSame(window.View.Root.Q<Button>("main-menu-new-game"), window.View.DefaultFocus);
            var paths = new SavePaths(GameRootTestHooks.SaveRoot);
            var slot = new SlotRef(Service<SaveService>().ActiveProfile, new SlotId("manual_001"));
            Directory.CreateDirectory(paths.SlotDir(slot)); File.WriteAllBytes(paths.WorldFile(slot), new byte[] { 1, 2, 3 });
            window.Presenter.Refresh(); Assert.IsFalse(window.Model.HasContinue, "A real corrupt slot must not become Continue.");
            window.Click("main-menu-load"); window.Click("main-menu-lab"); window.Click("main-menu-settings");
            Assert.AreEqual(1, window.Loads); Assert.AreEqual(1, window.Labs); Assert.AreEqual(1, window.Settings);
            window.Presenter.Unbind(); window.Click("main-menu-new-game"); Assert.IsFalse(window.UI.Prompt.IsOpen);
        }
        [UnityTest] public IEnumerator NewGame_EmptyName_OkDisabled()
        {
            using var window = Open(); window.Click("main-menu-new-game"); yield return null;
            foreach (string invalid in new[] { "", "   ", "Bad_Name", "Bad<Name", "Bad/Name" })
            {
                window.Field.value = invalid; Assert.IsFalse(window.Ok.enabledSelf, invalid);
                DialogTestContext.Enter(window.Field); DialogTestContext.Activate(window.Ok);
                Assert.IsTrue(window.UI.Prompt.IsOpen); Assert.IsFalse(Service<IGameFlow>().HasActiveSession);
            }
            Assert.AreEqual(24, window.Field.maxLength);
            Assert.IsNotNull(MainMenuPresenter.ValidateCommanderName(new string('A', 25)));
            window.Field.value = "Valid name"; Assert.IsTrue(window.Ok.enabledSelf);
            // An outstanding prompt from the previous binding cannot start a new session.
            window.Presenter.Unbind(); window.Presenter.Bind(); DialogTestContext.Activate(window.Ok);
            yield return null; Assert.IsFalse(window.UI.Prompt.IsOpen); Assert.IsFalse(Service<IGameFlow>().HasActiveSession);
            Assert.AreSame(window.Model, window.View.Root.dataSource);
        }
        [UnityTest] public IEnumerator NewGame_ValidName_CallsNewGame()
        {
            using var window = Open(); yield return null;
            window.Click("main-menu-new-game"); window.Click("main-menu-new-game"); // Second request must not queue a prompt.
            window.Field.value = "  Ada O'Neil  "; Assert.IsTrue(window.Ok.enabledSelf);
            DialogTestContext.Enter(window.Field);
            var flow = Service<IGameFlow>(); var session = Service<GameSession>();
            yield return Settle(window, () => flow.CurrentScene == GameScene.World && !flow.IsTransitioning && session.HasActiveSession);
            Assert.IsFalse(window.UI.Prompt.IsOpen); Assert.AreEqual("Ada O'Neil", session.CommanderName);
            Assert.IsFalse(session.IsCreativeSession); Assert.AreNotEqual(0UL, session.CampaignSeed);
            Assert.AreEqual(1, Service<GalaxyService>().GenerationCount, "The prompt requests one actual new game.");

            // Continue uses the actual persisted newest readable slot, including its commander/session data.
            var save = Service<SaveService>(); var first = new SlotRef(save.ActiveProfile, new SlotId("manual_001"));
            var latest = new SlotRef(save.ActiveProfile, new SlotId("manual_002"));
            yield return Complete(save.SaveAsync(first, SaveReason.Manual, "Earlier save"), result => Assert.IsTrue(result.Success, result.Message));
            yield return Complete(save.SaveAsync(latest, SaveReason.Manual, "Latest save"), result => Assert.IsTrue(result.Success, result.Message));
            yield return Complete(flow.QuitToMainMenuAsync(false), result => Assert.AreEqual(FlowResult.Ok, result));
            EventHub.DrainAll(); window.Presenter.Refresh();
            Assert.IsTrue(window.Model.HasContinue); Assert.AreEqual("Latest save", window.Model.ContinueSubtitle);
            Assert.AreEqual("Latest save", window.View.Root.Q<Label>("main-menu-continue-subtitle").text);
            window.Click("main-menu-continue");
            yield return Settle(window, () => flow.CurrentScene == GameScene.World && !flow.IsTransitioning && session.HasActiveSession);
            Assert.AreEqual(latest, save.CurrentSlot); Assert.AreEqual("Ada O'Neil", session.CommanderName);
        }
        [UnityTest] public IEnumerator Layout_AllLogicalSizes_NoIssues()
        {
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var window = Open(size.x, size.y);
                // Worst-case visible menu snapshot; no invented save record or scene owner.
                window.Model.HasContinue = true; window.Model.ContinueSubtitle = new string('W', 128); window.Model.VersionText = new string('V', 40);
                yield return null;
                Audit(window.View.Root, window.Panel.Viewport);
                Assert.AreEqual(420, window.View.Root.Q<VisualElement>("main-menu-column").worldBound.width, 1);
                window.Click("main-menu-new-game"); window.Field.value = "ABCDEFGHIJKLMNOPQRSTUVWX";
                yield return null; Audit(window.Prompt.Root, window.Panel.Viewport);
            }
        }
        [UnityTest] public IEnumerator Contrast_NameField_Passes()
        {
            using var window = Open(); window.Click("main-menu-new-game"); window.Field.value = "Ada"; yield return null;
            var issues = new List<ContrastIssue>(); ContrastAudit.Run(window.Prompt.Root, issues);
            Assert.IsEmpty(issues, string.Join("\n", issues.Select(issue => issue.ElementPath + " " + issue.Ratio + " < " + issue.Required)));
            Assert.IsTrue(window.Field.ClassListContains(UIStyleClasses.TextField));
            Assert.IsTrue(window.Field.Query<TextElement>().ToList().Any(element => !string.IsNullOrEmpty(element.text)), "Audit actual name-field text.");
            window.Field.value = "Bad_Name"; yield return null;
            ContrastAudit.Run(window.Prompt.Root, issues); Assert.IsEmpty(issues, "Invalid-field text and error must retain contrast.");
            Assert.IsFalse(window.Ok.enabledSelf);
        }
    }
}
