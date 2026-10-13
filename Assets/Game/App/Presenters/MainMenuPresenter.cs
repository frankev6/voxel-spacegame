using System;
using Game.Core;
using Game.Core.Flow;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using UnityEngine;

namespace Game.App.Presenters
{
    /// <summary>App-owned menu commands; only the flow service starts sessions or changes scenes.</summary>
    internal sealed class MainMenuPresenter : IMainMenuCommands, IDisposable
    {
        readonly UIRoot ui;
        readonly MainMenuViewModel model;
        readonly IGameFlow flow;
        readonly SaveService save;
        readonly Action showLoad, openLab, showSettings;
        Awaitable<FlowResult> pending;
        string continueSlot;
        bool bound, disposed;

        internal MainMenuPresenter(UIRoot ui, MainMenuViewModel model, IGameFlow flow, SaveService save,
            Action showLoad, Action openLab, Action showSettings)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.showLoad = showLoad ?? throw new ArgumentNullException(nameof(showLoad));
            this.openLab = openLab ?? throw new ArgumentNullException(nameof(openLab));
            this.showSettings = showSettings ?? throw new ArgumentNullException(nameof(showSettings));
        }
        internal void Bind()
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainMenuPresenter));
            if (bound) return;
            bound = true;
            ui.Navigator.StackChanged += OnStackChanged;
            Refresh();
        }
        internal void Unbind()
        {
            if (!bound) return;
            bound = false;
            ui.Navigator.StackChanged -= OnStackChanged;
        }
        void OnStackChanged()
        {
            if (ui.Navigator.Top is MainMenuView) Refresh();
        }
        internal void Refresh()
        {
            continueSlot = null;
            model.ContinueSubtitle = string.Empty;
            var slots = save.ListSlots(save.ActiveProfile);
            foreach (var slot in slots)
            {
                if (slot.Status == SlotStatus.Corrupt || slot.Status == SlotStatus.NewerFormat) continue;
                continueSlot = slot.Ref.Slot.Value;
                model.ContinueSubtitle = slot.Meta?.DisplayName ?? continueSlot;
                break;
            }
            model.HasContinue = continueSlot != null;
            model.VersionText = Application.version;
        }
        bool CanAct => !disposed && pending == null && !flow.IsTransitioning;
        public void Continue()
        {
            if (!CanAct) return;
            Refresh();
            if (continueSlot != null) pending = flow.LoadGameAsync(continueSlot);
        }
        public void NewGame()
        {
            if (!CanAct) return;
            ui.Prompt.Show(new TextPromptRequest
            {
                Title = "New game", Label = "Commander name", MaxLength = 24,
                Validate = ValidateCommanderName
            }, StartNewGame, null);
        }
        void StartNewGame(string name)
        {
            if (CanAct) pending = flow.NewGameAsync(new NewGameArgs(name, 0, false, ContentId.None));
        }
        internal static string ValidateCommanderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 24) return "Enter a commander name of 1–24 characters.";
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '\'')
                    return "Use letters, digits, spaces, hyphens or apostrophes.";
            return null;
        }
        public void Load() { if (CanAct) showLoad(); }
        public void OpenLab() { if (CanAct) openLab(); }
        public void OpenSettings() { if (CanAct) showSettings(); }
        public void Quit()
        {
            if (!CanAct) return;
            ui.Confirm.Show(new ConfirmRequest
            {
                Title = "Quit", Body = "Quit to desktop?", CancelIndex = 1, DefaultFocusIndex = 1,
                Options = new[]
                {
                    new ConfirmOption { Label = "Quit", Variant = ButtonVariant.Danger, Enabled = true },
                    new ConfirmOption { Label = "Cancel", Variant = ButtonVariant.Secondary, Enabled = true }
                }
            }, choice => { if (choice == 0 && CanAct) flow.QuitToDesktop(); });
        }
        internal void Tick()
        {
            if (disposed || pending == null) return;
            var awaiter = pending.GetAwaiter();
            if (!awaiter.IsCompleted) return;
            pending = null;
            try
            {
                var result = awaiter.GetResult();
                if (result != FlowResult.Ok)
                    ui.Notifications.Post(new Notification { Kind = NotificationKind.Danger, Label = "Unable to start the game: " + result });
            }
            catch (Exception error)
            {
                GameLog.Exception(LogCategories.Flow, error, "Main menu operation");
                ui.Notifications.Post(new Notification { Kind = NotificationKind.Danger, Label = "Unable to start the game." });
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            Unbind();
            disposed = true;
            pending = null;
        }
        // TODO(HUD-003): complete composition hookup and required name-entry/layout acceptance.
    }
}
