using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.App.Presenters;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Persistence.Data;
using Game.Persistence.Services;
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
    /// <summary>SPEC-61 HUD-004: real save files and runtime controls, controlled flow and bounded panel frames.</summary>
    public sealed class SaveSlotsViewTests
    {
        sealed class SaveContext : ISaveContext
        {
            internal bool Active = true;
            public bool HasActiveSession => Active;
            public SaveBlockReason GetBlockReason() => Active ? SaveBlockReason.None : SaveBlockReason.NoSession;
            public void PrepareCapture() { }
            public void DescribeForMeta(SaveMeta meta) { meta.CommanderName = "Tester"; meta.LocationText = new string('L', 40); meta.PlaytimeSeconds = 12345; }
            public bool TryCaptureSlotThumbnail(out ThumbnailRequest thumbnail) { thumbnail = null; return false; }
            public void CollectThumbnails(List<ThumbnailRequest> changed, HashSet<string> keep) { }
        }
        sealed class Saves : IDisposable
        {
            internal readonly SaveContext Context = new();
            internal readonly SavePaths Paths;
            internal readonly SaveService Service;
            readonly ContentService content;
            internal Saves(int count)
            {
                Paths = new SavePaths(Path.Combine(Application.temporaryCachePath, "hud004-" + Guid.NewGuid().ToString("N")));
                content = ContentService.CreateForTests(new ContentDefinition[] {
                    AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"),
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset") });
                Service = new SaveService(Paths, content, new DialogTestContext.Pause(), new SaveServiceConfig());
                Service.AttachContext(Context); Service.SetActiveProfile(Service.CreateProfile("HUD fixture").Id);
                for (int i = 0; i < count; i++)
                {
                    var slot = new SlotRef(Service.ActiveProfile, new SlotId("manual_" + (i + 1).ToString("D3")));
                    var capture = Service.CaptureNow(slot, SaveReason.Manual, "Save " + (i + 1) + " " + new string('N', 32));
                    Assert.IsFalse(capture.Failure.HasValue); Assert.IsTrue(Service.WriteCaptured(capture).Success);
                }
            }
            public void Dispose() { Service.Dispose(); content.Dispose(); if (Directory.Exists(Paths.Root)) Directory.Delete(Paths.Root, true); }
        }
        sealed class ControlledFlow : IGameFlow
        {
            readonly Saves saves;
            internal int Loads;
            internal ControlledFlow(Saves saves) { this.saves = saves; }
            public GameScene CurrentScene => GameScene.World;
            public bool IsTransitioning => false;
            public bool HasActiveSession => true;
            public bool IsCreativeSession => false;
            public SaveBlockReason GetSaveBlock() => SaveBlockReason.None;
            public Awaitable<FlowResult> LoadGameAsync(string id)
            {
                Loads++;
                var result = saves.Service.ReadNow(new SlotRef(saves.Service.ActiveProfile, new SlotId(id)));
                if (result.Success) saves.Service.Restore(result.Save);
                var completion = new AwaitableCompletionSource<FlowResult>();
                completion.SetResult(result.Success ? FlowResult.Ok : FlowResult.Failed); return completion.Awaitable;
            }
            public Awaitable<FlowResult> SaveGameAsync(string id, SaveReason reason) => throw new NotSupportedException();
            public Awaitable<FlowResult> NewGameAsync(NewGameArgs args) => throw new NotSupportedException();
            public Awaitable<FlowResult> QuitToMainMenuAsync(bool saveFirst) => throw new NotSupportedException();
            public Awaitable<FlowResult> EnterBlueprintLabAsync(BlueprintLabArgs args) => throw new NotSupportedException();
            public Awaitable<FlowResult> ExitBlueprintLabAsync() => throw new NotSupportedException();
            public void QuitToDesktop() => throw new NotSupportedException();
        }
        sealed class Window : IDisposable
        {
            internal readonly UITestPanel Panel;
            internal readonly UIRoot UI;
            internal readonly SaveSlotsViewModel Model = new();
            internal readonly SaveSlotsPresenter Presenter;
            internal readonly SaveSlotsView View;
            internal readonly ControlledFlow Flow;
            readonly UIConfig config;
            readonly Saves saves;
            internal Window(Saves saves, int width = 1280, int height = 720)
            {
                this.saves = saves;
                Panel = new UITestPanel(width, height);
                config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
                config.PanelSettings = Panel.Settings;
                UI = new UIRoot(Panel.Document, config, new DialogTestContext.Pause(), new DialogTestContext.Context(), new NullUISoundSink(), new UnityFrameClock());
                var root = new DialogTestContext.RootView(); UI.Register(root); UI.Navigator.SetRoot(root);
                Flow = new ControlledFlow(saves); Presenter = new SaveSlotsPresenter(UI, Model, saves.Service, Flow);
                View = new SaveSlotsView(Model, Presenter); UI.Register(View); Presenter.Bind();
            }
            internal void Show(SaveSlotsMode mode) { Presenter.Show(mode); Presenter.Select(0); }
            internal SlotRef Selected => new SlotRef(saves.Service.ActiveProfile, new SlotId(Model.Slots[Model.SelectedIndex].SlotId));
            internal void Click(string name) => DialogTestContext.Activate(View.Root.Q<Button>(name));
            internal void Accept() => DialogTestContext.Activate(UI.Navigator.Get<ConfirmDialogView>().Root.Q<Button>("save-slots-confirm-action"));
            public void Dispose() { Presenter.Dispose(); UI.Dispose(); Object.Destroy(config); Panel.Dispose(); }
        }

        [UnityTest] public IEnumerator Delete_RequiresConfirm()
        {
            using var data = new Saves(1); using var window = new Window(data); window.Show(SaveSlotsMode.Load);
            yield return null;
            var slot = window.Selected; window.Click("save-slots-delete");
            Assert.IsTrue(window.UI.Confirm.IsOpen); Assert.IsTrue(File.Exists(data.Paths.WorldFile(slot)));
            window.Accept();
            Assert.IsFalse(Directory.Exists(data.Paths.SlotDir(slot))); Assert.AreEqual(0, window.Model.Slots.Count);
        }
        [UnityTest] public IEnumerator Delete_EscKeepsSlot()
        {
            using var data = new Saves(1); using var window = new Window(data); window.Show(SaveSlotsMode.Load);
            yield return null;
            var slot = window.Selected; byte[] before = File.ReadAllBytes(data.Paths.WorldFile(slot));
            window.Click("save-slots-delete"); Assert.IsTrue(window.UI.Confirm.IsOpen);
            // This is the real Back entry bound to Escape by app composition, not a private confirmation callback.
            window.UI.HandleBackPressed();
            Assert.IsFalse(window.UI.Confirm.IsOpen); CollectionAssert.AreEqual(before, File.ReadAllBytes(data.Paths.WorldFile(slot)));
            Assert.AreEqual(1, window.Model.Slots.Count);
        }
        [UnityTest] public IEnumerator Overwrite_RequiresConfirm()
        {
            using var data = new Saves(1); using var window = new Window(data); window.Show(SaveSlotsMode.Save);
            yield return null;
            var slot = window.Selected; byte[] before = File.ReadAllBytes(data.Paths.WorldFile(slot));
            window.Click("save-slots-save");
            Assert.IsTrue(window.UI.Confirm.IsOpen); Assert.IsFalse(data.Service.IsBusy);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(data.Paths.WorldFile(slot)));
            window.UI.HandleBackPressed(); CollectionAssert.AreEqual(before, File.ReadAllBytes(data.Paths.WorldFile(slot)));
            window.UI.Navigator.Pop(window.View); window.Show(SaveSlotsMode.Load);
            window.Click("save-slots-load"); Assert.IsTrue(window.UI.Confirm.IsOpen); Assert.AreEqual(0, window.Flow.Loads);
            window.Accept(); Assert.AreEqual(1, window.Flow.Loads); window.Presenter.Tick();
            Assert.IsFalse(window.View.IsOpen);
        }
        [UnityTest] public IEnumerator SaveError_ShowsDangerToast()
        {
            using var data = new Saves(1); using var window = new Window(data); window.Show(SaveSlotsMode.Save);
            yield return null;
            window.Click("save-slots-save"); Assert.IsTrue(window.UI.Confirm.IsOpen);
            // The actual provider refuses capture after the confirmation was displayed. No fake completion event.
            data.Context.Active = false;
            window.Accept(); window.Presenter.Tick();
            Assert.AreEqual(1, window.UI.Notifications.VisibleCount); Assert.IsTrue(window.UI.Confirm.IsOpen);
            yield return null;
            Assert.IsTrue(window.UI.RootElement.Query<VisualElement>(className: UIStyleClasses.ToastDanger).ToList()
                .Any(element => !element.ClassListContains(UIStyleClasses.IsHidden)));
            Assert.IsNotEmpty(window.UI.Navigator.Get<ConfirmDialogView>().Root.Q<Label>("confirm-body").text);
        }
        [UnityTest] public IEnumerator Layout_64Slots_AllLogicalSizes_NoIssues()
        {
            using var data = new Saves(64); // Seed once; reuse the real slot store at all five sizes.
            var issues = new List<LayoutIssue>();
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var window = new Window(data, size.x, size.y); window.Show(SaveSlotsMode.Load);
                yield return null;
                Assert.AreEqual(64, window.Model.Slots.Count);
                var list = window.View.Root.Q<ListView>("save-slots-list");
                Assert.Greater(list.Q<ScrollView>().verticalScroller.highValue, 0);
                list.ScrollToItem(63); yield return null;
                var realized = list.Query<VisualElement>("save-slot-row").ToList();
                Assert.IsTrue(realized.Any(row => row.parent.dataSource is SaveSlotItemViewModel item && item.SlotId == window.Model.Slots[63].SlotId), "The last saved game is reachable by scrolling.");
                Assert.Less(realized.Count, 64, "Rows must be virtualized.");
                UILayoutAudit.Run(window.View.Root, window.Panel.Viewport, issues);
                Assert.IsEmpty(issues, size + ": " + string.Join(", ", issues.Select(issue => issue.Kind + " " + issue.ElementPath)));
            }
        }
    }
}
