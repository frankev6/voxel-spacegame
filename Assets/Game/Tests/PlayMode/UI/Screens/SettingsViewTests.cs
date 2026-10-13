using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.App.Presenters;
using Game.App.Settings;
using SettingsService = Game.App.Settings.SettingsService;
using Game.Core;
using Game.Core.Events;
using Game.Core.Settings;
using Game.Input;
using Game.Persistence.Container;
using Game.Presentation.Data;
using Game.Presentation.Services;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI.Screens
{
    /// <summary>SPEC-61 HUD-005: real files, real input with controlled time, and bounded panel frames.</summary>
    public sealed class SettingsViewTests : InputTestFixture
    {
        Keyboard keyboard;
        [SetUp] public override void Setup()
        { base.Setup(); EventHub.Reset(); Services.Reset(); keyboard = InputSystem.AddDevice<Keyboard>(); }
        [TearDown] public override void TearDown()
        { try { EventHub.Reset(); Services.Reset(); } finally { base.TearDown(); } }
        sealed class Window : IDisposable
        {
            internal readonly UITestPanel Panel;
            internal readonly UIRoot UI;
            internal readonly SettingsViewModel Model = new();
            internal readonly SettingsService Settings;
            internal readonly InputService Input;
            internal readonly SettingsPresenter Presenter;
            internal readonly SettingsView View;
            internal readonly SettingsPaths Paths;
            readonly InputServiceBridge bridge;
            readonly UIConfig config;
            internal Window(Keyboard keyboard, int width = 1280, int height = 720)
            {
                Paths = new SettingsPaths(Path.Combine(Application.temporaryCachePath, "hud005-" + Guid.NewGuid().ToString("N"), "settings.json"));
                Settings = new SettingsService(new NewtonsoftSaveJsonCodec(), Paths);
                Input = new InputService(Settings.Current.Controls); Input.Controls.asset.devices = new InputDevice[] { keyboard };
                bridge = new InputServiceBridge(Input); Panel = new UITestPanel(width, height);
                config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset")); config.PanelSettings = Panel.Settings;
                UI = new UIRoot(Panel.Document, config, new DialogTestContext.Pause(), bridge, new NullUISoundSink(), new UnityFrameClock());
                bridge.BindBack(UI.HandleBackPressed);
                var root = new DialogTestContext.RootView(); UI.Register(root); UI.Navigator.SetRoot(root);
                Presenter = new SettingsPresenter(UI, Model, Settings, Input); View = new SettingsView(Model, Presenter);
                UI.Register(View); Presenter.Bind(); Presenter.Show();
            }
            internal int InteractRow => Model.Rebinds.FindIndex(row => row.ActionPath == "Flight/Interact");
            internal void Click(string name) => DialogTestContext.Activate(View.Root.Q<Button>(name));
            public void Dispose()
            {
                Presenter.Dispose(); UI.Dispose(); bridge.Dispose(); Input.Dispose(); Settings.Dispose();
                Object.Destroy(config); Panel.Dispose(); if (Directory.Exists(Path.GetDirectoryName(Paths.FilePath))) Directory.Delete(Path.GetDirectoryName(Paths.FilePath), true);
            }
        }
        [UnityTest] public IEnumerator Slider_PersistsImmediately()
        {
            using var window = new Window(keyboard); yield return null;
            window.View.Root.Q<Slider>("settings-master-volume").value = .23f;
            Assert.AreEqual(.23f, window.Settings.Current.Audio.MasterVolume);
            Assert.IsTrue(File.Exists(window.Paths.FilePath), "Slider edits flush without waiting for debounce.");
            using (var loaded = new SettingsService(new NewtonsoftSaveJsonCodec(), window.Paths))
            { loaded.Load(); Assert.AreEqual(.23f, loaded.Current.Audio.MasterVolume); }
            // ADR-0073: exercise the persisted field and actual camera consumer inside this required case.
            window.Presenter.SelectTab(3); window.View.Root.Q<DropdownField>("settings-camera-yaw-mode").value = "Free orbit";
            Assert.AreEqual(1, window.Settings.Current.Controls.CameraYawModeIndex);
            using var restored = new SettingsService(new NewtonsoftSaveJsonCodec(), window.Paths); restored.Load();
            Assert.AreEqual(1, restored.Current.Controls.CameraYawModeIndex);
            var config = ScriptableObject.CreateInstance<CameraConfig>(); var prefab = new GameObject("HUD005 camera fixture"); prefab.AddComponent<Camera>(); config.CameraPrefab = prefab;
            try
            {
                using var camera = new CameraRigService(config, null, null); Services.Register(camera);
                SettingsApplier.Apply(SettingsSection.Controls, restored.Current);
                camera.SetMode(CameraMode.Flight, true); camera.SetTarget(float3.zero, quaternion.RotateY(math.radians(90)), float3.zero, 10, true); camera.Tick(0);
                Assert.AreEqual(0, camera.YawDeg, .001f, "Free orbit does not follow the ship heading.");
                window.View.Root.Q<DropdownField>("settings-camera-yaw-mode").value = "Follow heading";
                SettingsApplier.Apply(SettingsSection.Controls, window.Settings.Current);
                camera.SetTarget(float3.zero, quaternion.RotateY(math.radians(90)), float3.zero, 10, true); camera.Tick(0);
                Assert.AreEqual(90, camera.YawDeg, .001f); Services.Unregister(camera);
            }
            finally { Object.Destroy(config); Object.Destroy(prefab); }
            var invalid = window.Settings.CreateEditableCopy(); invalid.Controls.CameraYawModeIndex = 255; invalid.Clamp(); Assert.AreEqual(0, invalid.Controls.CameraYawModeIndex);
            window.Presenter.SetFloat(SettingKey.UiScale, 1.15f); Assert.AreEqual(1.15f, window.Panel.Settings.scale);
            window.Presenter.SetIndex(SettingKey.TextScale, 2); window.Presenter.SetBool(SettingKey.ReduceMotion, true);
            Assert.IsTrue(window.UI.ReduceMotion); Assert.IsTrue(window.UI.RootElement.ClassListContains(UIStyleClasses.TextScale130));
        }
        [UnityTest] public IEnumerator Rebind_EscCancels_ScreenStays()
        {
            using var window = new Window(keyboard); window.Presenter.SelectTab(2); yield return null;
            int row = window.InteractRow; Assert.GreaterOrEqual(row, 0);
            var list = window.View.Root.Q<ListView>("settings-rebinds"); list.ScrollToItem(row); yield return null;
            window.Click("settings-rebind-" + row); Assert.IsTrue(window.Input.IsRebinding);
            Assert.AreEqual("Press a key…", window.Model.Rebinds[row].BindingText);
            Press(keyboard.escapeKey); InputSystem.Update(); Release(keyboard.escapeKey);
            Assert.IsFalse(window.Input.IsRebinding); Assert.IsTrue(window.View.IsOpen); Assert.AreEqual(-1, window.Model.ListeningRow);
            Assert.AreEqual("F", window.Model.Rebinds[row].BindingText); Assert.AreEqual(InputContext.Menu, window.Input.CurrentContext);
            window.Presenter.StartRebind(row); Assert.IsTrue(window.Input.IsRebinding);
            window.UI.Confirm.Show(DialogTestContext.Confirm(), null);
            Assert.IsFalse(window.Input.IsRebinding, "Covering settings cancels its listening operation.");
            Assert.AreEqual(-1, window.Model.ListeningRow); Assert.IsTrue(window.Input.Controls.Global.enabled);
            yield return null; PressAndRelease(keyboard.escapeKey);
            Assert.IsFalse(window.UI.Confirm.IsOpen); Assert.IsTrue(window.View.IsOpen);
            window.Presenter.StartRebind(row); Assert.IsTrue(window.Input.IsRebinding);
            window.Presenter.Unbind(); Assert.IsFalse(window.Input.IsRebinding); Assert.AreEqual(-1, window.Model.ListeningRow);
            var model = window.View.Root.dataSource;
            window.Presenter.Bind(); Assert.AreSame(model, window.Model, "Session rebinding preserves the model identity.");
            yield return null; PressAndRelease(keyboard.escapeKey); Assert.IsFalse(window.View.IsOpen, "Back resumes normal navigation after cancellation.");
            // An unrelated input operation remains owned by its caller while settings is closed.
            window.Input.StartRebind("Flight/Interact", 0, _ => { });
            window.Presenter.CancelRebind(); Assert.IsTrue(window.Input.IsRebinding);
            window.Input.CancelRebind();
        }
        [UnityTest] public IEnumerator Rebind_Success_UpdatesRow()
        {
            using var window = new Window(keyboard); window.Presenter.SelectTab(2); yield return null;
            int row = window.InteractRow; window.View.Root.Q<ListView>("settings-rebinds").ScrollToItem(row); yield return null;
            window.Click("settings-rebind-" + row); Press(keyboard.uKey); currentTime += .2; InputSystem.Update(); Release(keyboard.uKey);
            Assert.IsFalse(window.Input.IsRebinding); Assert.AreEqual("U", window.Model.Rebinds[row].BindingText);
            yield return null;
            Assert.AreEqual("U", window.View.Root.Query<Label>("settings-rebind-binding").ToList().Single(label => label.parent.parent.userData is int index && index == row).text);
            using var loaded = new SettingsService(new NewtonsoftSaveJsonCodec(), window.Paths); loaded.Load();
            using var restored = new InputService(loaded.Current.Controls); Assert.AreEqual("U", restored.GetBindingDisplay("Flight/Interact"));
            window.Click("settings-reset-bindings"); Assert.AreEqual("F", window.Model.Rebinds[row].BindingText);
            loaded.Load(); restored.ApplyBindingOverrides(loaded.Current.Controls.BindingOverridesJson); Assert.AreEqual("F", restored.GetBindingDisplay("Flight/Interact"));
        }
        [UnityTest] public IEnumerator RebindList_ExcludesPointerAndComposites()
        {
            using var window = new Window(keyboard); yield return null;
            var expected = new List<string>();
            foreach (var map in window.Input.Controls.asset.actionMaps)
            {
                if (!new[] { "Global", "Flight", "Editor", "Dialog" }.Contains(map.name)) continue;
                foreach (var action in map.actions)
                    foreach (var binding in action.bindings)
                    {
                        if (binding.isComposite || binding.isPartOfComposite || binding.path == "<Mouse>/position" || binding.path == "<Mouse>/delta" || binding.path == "<Mouse>/scroll") continue;
                        expected.Add(map.name + "/" + action.name);
                    }
            }
            CollectionAssert.AreEqual(expected, window.Model.Rebinds.Select(row => row.ActionPath));
            Assert.IsTrue(window.Model.Rebinds.Any(row => row.ActionPath == "Flight/FirePrimary"));
            Assert.IsFalse(window.Model.Rebinds.Any(row => row.ActionPath.StartsWith("UI/", StringComparison.Ordinal) || row.ActionPath.StartsWith("Menu/", StringComparison.Ordinal)));
        }
        [UnityTest] public IEnumerator Layout_AllTabs_AllLogicalSizes_NoIssues()
        {
            var issues = new List<LayoutIssue>();
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var window = new Window(keyboard, size.x, size.y); yield return null;
                for (int tab = 0; tab < 5; tab++)
                {
                    window.View.Root.Q<TabBar>("settings-tabs").SelectedIndex = tab; yield return null;
                    Assert.AreEqual(tab, window.Model.SelectedTab);
                    var pages = window.View.Root.Query<VisualElement>(className: "settings-page").ToList();
                    Assert.AreEqual(1, pages.Count(page => !page.ClassListContains(UIStyleClasses.IsHidden)));
                    Assert.IsFalse(pages[tab].ClassListContains(UIStyleClasses.IsHidden));
                    UILayoutAudit.Run(window.View.Root, window.Panel.Viewport, issues);
                    Assert.IsEmpty(issues, size + " tab " + tab + ": " + string.Join(", ", issues.Select(issue => issue.Kind + " " + issue.ElementPath)));
                }
            }
        }
    }
}
