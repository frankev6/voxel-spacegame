using System;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Save;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using UnityEngine;

namespace Game.App.Presenters
{
    /// <summary>Pause actions use the navigator's token and the session's save eligibility.</summary>
    internal sealed class PausePresenter : IPauseCommands, IDisposable
    {
        readonly UIRoot ui;
        readonly PauseViewModel model;
        readonly IGameFlow flow;
        readonly Action<SaveSlotsMode> showSlots;
        readonly Action showSettings;
        Awaitable<FlowResult> pending;
        bool bound, disposed;
        uint generation, operationGeneration;
        internal PausePresenter(UIRoot ui, PauseViewModel model, IGameFlow flow,
            Action<SaveSlotsMode> showSlots, Action showSettings)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.showSlots = showSlots ?? throw new ArgumentNullException(nameof(showSlots));
            this.showSettings = showSettings ?? throw new ArgumentNullException(nameof(showSettings));
        }
        internal void Bind()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PausePresenter));
            if (bound) return;
            bound = true;
            ui.Navigator.StackChanged += OnStackChanged;
            Refresh();
        }
        internal void Unbind()
        {
            if (!bound) return;
            bound = false; generation++;
            ui.Navigator.StackChanged -= OnStackChanged;
        }
        void OnStackChanged() { if (ui.Navigator.Top is PauseView) Refresh(); }
        internal void Refresh()
        {
            var reason = flow.GetSaveBlock();
            model.CanSave = reason == SaveBlockReason.None;
            model.SaveBlockedReason = reason switch
            {
                SaveBlockReason.None => string.Empty,
                SaveBlockReason.NoSession => "No active game to save.",
                SaveBlockReason.InCombat => "Cannot save during combat.",
                SaveBlockReason.Transitioning => "Wait for the scene transition to finish.",
                SaveBlockReason.PlayerDead => "Cannot save a destroyed player ship.",
                SaveBlockReason.EditorOpen => "Finish editing before saving.",
                _ => "Saving is unavailable."
            };
        }
        bool CanAct => bound && !disposed && pending == null && !flow.IsTransitioning && ui.Navigator.IsOpen<PauseView>();
        public void Resume() { if (CanAct) ui.Navigator.Pop(ui.Navigator.Get<PauseView>()); }
        public void Save()
        {
            if (!CanAct) return;
            Refresh();
            if (model.CanSave) showSlots(SaveSlotsMode.Save);
        }
        public void Load() { if (CanAct) showSlots(SaveSlotsMode.Load); }
        public void OpenSettings() { if (CanAct) showSettings(); }
        public void MainMenu()
        {
            if (!CanAct) return;
            // QA-001: a blocked save (dialogue, combat) must not trap the player in the world; say so and offer to leave unsaved.
            Refresh(); bool save = model.CanSave;
            Confirm("Main menu", save ? "Save and return to the main menu?"
                : model.SaveBlockedReason + " Return to the main menu without saving? Progress since your last save is lost.",
                save ? "Main menu" : "Leave without saving", () =>
            {
                if (CanAct)
                {
                    operationGeneration = generation;
                    pending = flow.QuitToMainMenuAsync(saveFirst: save);
                }
            });
        }
        public void Quit()
        {
            if (CanAct) Confirm("Quit", "Quit to desktop?", "Quit", () => { if (CanAct) flow.QuitToDesktop(); });
        }
        void Confirm(string title, string body, string accept, Action action)
        {
            uint version = generation;
            ui.Confirm.Show(new ConfirmRequest
            {
                Title = title, Body = body, CancelIndex = 1, DefaultFocusIndex = 1,
                Options = new[]
                {
                    new ConfirmOption { Label = accept, Enabled = true, Variant = ButtonVariant.Danger },
                    new ConfirmOption { Label = "Cancel", Enabled = true, Variant = ButtonVariant.Secondary }
                }
            }, choice => { if (bound && !disposed && version == generation && choice == 0) action(); });
        }
        internal void Tick()
        {
            if (disposed) return;
            if (ui.Navigator.Top is PauseView) Refresh();
            if (pending == null) return;
            var awaiter = pending.GetAwaiter();
            if (!awaiter.IsCompleted) return;
            pending = null;
            try
            {
                var result = awaiter.GetResult();
                if (bound && operationGeneration == generation && result != FlowResult.Ok)
                    ui.Notifications.Post(new Notification { Kind = NotificationKind.Danger, Label = "Unable to return to the main menu: " + result });
            }
            catch (Exception error)
            {
                GameLog.Exception(LogCategories.Flow, error, "Pause menu operation");
                if (bound && operationGeneration == generation)
                    ui.Notifications.Post(new Notification { Kind = NotificationKind.Danger, Label = "Unable to return to the main menu." });
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            Unbind(); disposed = true; pending = null;
        }
    }
}
