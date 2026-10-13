using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using UnityEngine;

namespace Game.App.Presenters
{
    /// <summary>SPEC-61 HUD-004: real slot operations, immutable confirmation targets and app-owned model lifetime.</summary>
    public sealed class SaveSlotsPresenter : ISaveSlotsCommands, IDisposable
    {
        readonly UIRoot ui;
        readonly SaveSlotsViewModel model;
        readonly SaveService save;
        readonly IGameFlow flow;
        readonly List<SlotInfo> slots = new(64);
        readonly List<SaveSlotItemViewModel> rows = new(64);
        Awaitable<SaveResult> saving;
        Awaitable<FlowResult> loading;
        SlotRef target;
        string operationError;
        uint generation;
        bool bound, disposed, ownsOperation;
        public SaveSlotsPresenter(UIRoot ui, SaveSlotsViewModel model, SaveService save, IGameFlow flow)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }
        public void Bind()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SaveSlotsPresenter));
            if (bound) return;
            bound = true; save.OperationCompleted += Completed;
            EventBus<GameSaved>.Subscribe(OnGameSaved); ui.Navigator.StackChanged += StackChanged;
            Refresh();
        }
        public void Unbind()
        {
            if (!bound) return;
            bound = false; generation++;
            save.OperationCompleted -= Completed; EventBus<GameSaved>.Unsubscribe(OnGameSaved);
            ui.Navigator.StackChanged -= StackChanged;
        }
        public void Show(SaveSlotsMode mode)
        {
            if (disposed || !bound || ownsOperation || flow.IsTransitioning || ui.Confirm.IsOpen || ui.Prompt.IsOpen) return;
            generation++; model.Mode = mode; Refresh();
            if (!ui.Navigator.IsOpen<SaveSlotsView>()) ui.Navigator.Push<SaveSlotsView>(mode);
        }
        void StackChanged() { if (ui.Navigator.Top is SaveSlotsView) Refresh(); }
        void OnGameSaved(GameSaved e) { if (e.ProfileId == save.ActiveProfile.Value) Refresh(); }
        public void Refresh()
        {
            if (disposed) return;
            SlotRef? selected = Selected?.Ref;
            slots.Clear(); foreach (var slot in save.ListSlots(save.ActiveProfile)) slots.Add(slot);
            model.Slots.Clear(); int selection = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i]; if (i == rows.Count) rows.Add(new SaveSlotItemViewModel());
                var row = rows[i]; row.SlotId = slot.Ref.Slot.Value;
                row.Title = slot.Meta?.DisplayName ?? "Unreadable save";
                row.LocationText = slot.Status == SlotStatus.Corrupt ? "This save cannot be read." :
                    slot.Status == SlotStatus.NewerFormat ? "This save needs a newer game version." : slot.Meta?.LocationText ?? "";
                row.TimestampText = slot.Meta == null ? "" : slot.Meta.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                double seconds = slot.Meta?.PlaytimeSeconds ?? 0;
                row.PlaytimeText = double.IsFinite(seconds) && seconds >= 0 && seconds < TimeSpan.MaxValue.TotalSeconds
                    ? ((long)(seconds / 3600)).ToString(CultureInfo.InvariantCulture) + "h " + ((int)(seconds / 60) % 60).ToString("D2", CultureInfo.InvariantCulture) + "m" : "";
                row.IsAutosave = slot.Kind == SlotKind.Auto;
                if (selected.HasValue && slot.Ref.Equals(selected.Value)) selection = i;
                model.Slots.Add(row);
            }
            model.SelectedIndex = selection;
            for (int i = 0; i < model.Slots.Count; i++) model.Slots[i].IsSelected = i == selection;
            model.EmptyText = "No saved games";
            UpdateActions(); model.NotifySlots();
        }
        SlotInfo Selected => model.SelectedIndex >= 0 && model.SelectedIndex < slots.Count ? slots[model.SelectedIndex] : null;
        bool Ready => !disposed && bound && !ownsOperation && !save.IsBusy && !flow.IsTransitioning;
        bool OnScreen => Ready && ui.Navigator.IsOpen<SaveSlotsView>() && !ui.Confirm.IsOpen && !ui.Prompt.IsOpen;
        bool CanSave => Ready && flow.HasActiveSession && flow.CurrentScene == GameScene.World && flow.GetSaveBlock() == SaveBlockReason.None;
        void UpdateActions()
        {
            var slot = Selected;
            model.CanSave = model.Mode == SaveSlotsMode.Save && CanSave;
            model.CanLoad = model.Mode == SaveSlotsMode.Load && Ready && slot != null &&
                slot.Status != SlotStatus.Corrupt && slot.Status != SlotStatus.NewerFormat;
            model.CanDelete = Ready && slot != null;
        }
        public void Select(int index)
        {
            if (disposed || index < -1 || index >= slots.Count) return;
            model.SelectedIndex = index;
            for (int i = 0; i < model.Slots.Count; i++) model.Slots[i].IsSelected = i == index;
            UpdateActions(); model.NotifySlots();
        }
        void Confirm(string title, string body, string action, SlotRef slot, Action accepted)
        {
            uint version = generation;
            ui.Confirm.Show(new ConfirmRequest
            {
                Title = title, Body = body, CancelIndex = 1, DefaultFocusIndex = 1,
                Options = new[] {
                    new ConfirmOption { Label = action, ElementName = "save-slots-confirm-action", Variant = ButtonVariant.Danger, Enabled = true },
                    new ConfirmOption { Label = "Cancel", ElementName = "save-slots-confirm-cancel", Variant = ButtonVariant.Secondary, Enabled = true } }
            }, choice => { if (choice == 0 && version == generation && OnScreen && slot.Profile.Equals(save.ActiveProfile)) accepted(); });
        }
        public void DeleteSelected()
        {
            if (!OnScreen || Selected == null) return;
            var slot = Selected;
            Confirm("Delete save", "Delete “" + (slot.Meta?.DisplayName ?? "this saved game") + "”? This cannot be undone.", "Delete", slot.Ref, () =>
            {
                try { if (!save.DeleteSlot(slot.Ref)) Error("The saved game could not be deleted."); }
                catch (Exception e) { Error("The saved game could not be deleted: " + e.Message); }
                Refresh();
            });
        }
        public void SaveToSelected()
        {
            if (!OnScreen || !CanSave || Selected == null || model.Mode != SaveSlotsMode.Save) return;
            var slot = Selected;
            Confirm("Overwrite save", "Overwrite “" + (slot.Meta?.DisplayName ?? "this saved game") + "”?", "Overwrite", slot.Ref,
                () => { if (CanSave) StartSave(slot.Ref, slot.Meta?.DisplayName); });
        }
        public void LoadSelected()
        {
            if (!OnScreen || !model.CanLoad || Selected == null) return;
            var slot = Selected;
            if (flow.CurrentScene == GameScene.World)
                Confirm("Load game", "Unsaved progress since the last save will be lost.", "Load", slot.Ref, () => StartLoad(slot.Ref));
            else StartLoad(slot.Ref);
        }
        public void SaveNew()
        {
            if (!OnScreen || !CanSave || model.Mode != SaveSlotsMode.Save) return;
            var profile = save.ActiveProfile; uint version = generation;
            ui.Prompt.Show(new TextPromptRequest { Title = "New save", Label = "Save name", MaxLength = 32, OkLabel = "Save" }, name =>
            {
                if (version != generation || !OnScreen || !CanSave || !profile.Equals(save.ActiveProfile)) return;
                if (!save.TryCreateManualSlot(profile, out var slot)) { Error("There are no free manual save slots."); return; }
                StartSave(slot, name);
            }, null);
        }
        void StartSave(SlotRef slot, string displayName)
        {
            target = slot; ownsOperation = true; operationError = null;
            try
            {
                // IGameFlow has no display-name parameter. The owning save API preserves the named slot without a contract change.
                saving = save.SaveAsync(slot, SaveReason.Manual, displayName);
            }
            catch (Exception e) { ownsOperation = false; Error("Unable to save: " + e.Message); }
            UpdateActions();
        }
        void StartLoad(SlotRef slot)
        {
            target = slot; ownsOperation = true; operationError = null;
            try { loading = flow.LoadGameAsync(slot.Slot.Value); }
            catch (Exception e) { ownsOperation = false; Error("Unable to load: " + e.Message); }
            UpdateActions();
        }
        void Completed(SaveOperationResult result)
        {
            if (!ownsOperation || !result.Slot.Equals(target)) return;
            if (!result.Success) operationError = result.Message;
        }
        public void Tick()
        {
            if (disposed) return;
            try
            {
                if (saving != null && saving.GetAwaiter().IsCompleted)
                {
                    var pending = saving; saving = null; var result = pending.GetAwaiter().GetResult();
                    ownsOperation = false;
                    if (!result.Success) Error(result.Message);
                    Refresh();
                }
                else if (loading != null && loading.GetAwaiter().IsCompleted)
                {
                    var pending = loading; loading = null; var result = pending.GetAwaiter().GetResult();
                    ownsOperation = false;
                    if (result != FlowResult.Ok) Error(operationError ?? "Unable to load: " + result);
                    else if (ui.Navigator.IsOpen<SaveSlotsView>()) ui.Navigator.Pop(ui.Navigator.Get<SaveSlotsView>());
                    Refresh();
                }
                else if (ui.Navigator.IsOpen<SaveSlotsView>()) UpdateActions();
            }
            catch (Exception e) { ownsOperation = false; saving = null; loading = null; Error("Save operation failed: " + e.Message); Refresh(); }
        }
        void Error(string message)
        {
            if (disposed) return;
            ui.Notifications.Post(new Notification { Kind = NotificationKind.Danger, Label = message });
            ui.Confirm.Show(new ConfirmRequest { Title = "Save operation failed", Body = message, CancelIndex = 0,
                Options = new[] { new ConfirmOption { Label = "OK", Variant = ButtonVariant.Primary, Enabled = true } } }, null);
        }
        public void Dispose() { if (disposed) return; Unbind(); disposed = true; slots.Clear(); rows.Clear(); }
    }
}
