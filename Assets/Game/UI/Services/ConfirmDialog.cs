using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;
using Game.UI.Views;
using UnityEngine.UIElements;

namespace Game.UI.Services
{
    /// <summary>SPEC-60 §5.9: cached modal, protected choices and FIFO requests; callbacks follow navigator removal.</summary>
    public sealed class ConfirmDialog
    {
        sealed class Pending
        {
            internal ConfirmRequest Request;
            internal Action<int> Chosen;
            internal int? Choice;
        }
        readonly UINavigator navigator;
        readonly TooltipService tooltips;
        readonly ConfirmDialogViewModel model = new ConfirmDialogViewModel();
        readonly ConfirmDialogView view;
        readonly Queue<Pending> queue = new Queue<Pending>();
        readonly HashSet<Button> tooltipButtons = new HashSet<Button>();
        Pending active, completed;
        bool disposed;
        internal ConfirmDialog(UINavigator navigator, TooltipService tooltips)
        {
            this.navigator = navigator; this.tooltips = tooltips;
            view = new ConfirmDialogView(model, Choose, Back, Popped, ConfigureTooltip);
            navigator.Register(view); navigator.StackChanged += StackChanged;
        }
        public bool IsOpen => active != null;
        public void Show(ConfirmRequest request, Action<int> onChosen)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ConfirmDialog));
            if (request == null || request.Options == null || request.Options.Length < 1 || request.Options.Length > 3)
                throw new ArgumentException("Confirm requires one to three options.", nameof(request));
            if (request.CancelIndex < -1 || request.CancelIndex >= request.Options.Length || request.DefaultFocusIndex < 0 || request.DefaultFocusIndex >= request.Options.Length)
                throw new ArgumentException("Confirm cancel/focus indices must refer to options.", nameof(request));
            var options = (ConfirmOption[])request.Options.Clone();
            for (int i = 0; i < options.Length; i++)
            {
                string name = options[i].ElementName ?? "confirm-option-" + i;
                if (name.Length == 0 || name == "screen-confirm" || name == "confirm-title" || name == "confirm-body" || name == "confirm-scrim" || name == "confirm-window" || name == "confirm-actions" || name == "confirm-scroll")
                    throw new ArgumentException("Invalid confirm option name.", nameof(request));
                for (int j = 0; j < 3; j++)
                    if (j != i && name == "confirm-option-" + j) throw new ArgumentException("Option name is reserved for another slot.", nameof(request));
                for (int k = 0; k < name.Length; k++)
                    if (!(name[k] >= 'a' && name[k] <= 'z') && !(name[k] >= '0' && name[k] <= '9') && name[k] != '-')
                        throw new ArgumentException("Confirm option names must be kebab-case.", nameof(request));
                for (int j = 0; j < i; j++)
                    if (name == options[j].ElementName) throw new ArgumentException("Duplicate confirm option name.", nameof(request));
                if (!Enum.IsDefined(typeof(ButtonVariant), options[i].Variant)) throw new ArgumentException("Invalid button variant.", nameof(request));
                options[i].ElementName = name;
            }
            var next = new Pending { Request = new ConfirmRequest { Title = request.Title, Body = request.Body, Options = options,
                CancelIndex = request.CancelIndex, DefaultFocusIndex = request.DefaultFocusIndex }, Chosen = onChosen };
            if (active != null) queue.Enqueue(next); else Begin(next);
        }
        void Begin(Pending next)
        {
            active = next; model.Title = next.Request.Title ?? ""; model.Body = next.Request.Body ?? "";
            model.DefaultFocusIndex = next.Request.DefaultFocusIndex;
            for (int i = 0; i < model.Options.Length; i++)
            {
                var option = model.Options[i]; option.Visible = i < next.Request.Options.Length;
                var data = option.Visible ? next.Request.Options[i] : default;
                option.Label = data.Label ?? ""; option.ElementName = option.Visible ? data.ElementName : "confirm-option-" + i;
                option.Variant = data.Variant; option.Enabled = data.Enabled; option.DisabledReason = data.DisabledReason ?? "";
            }
            try { navigator.Push(view); }
            catch { active = null; throw; }
        }
        void Choose(int index)
        {
            if (disposed || active == null || !view.IsOpen || active.Choice.HasValue || index < 0 || index >= active.Request.Options.Length || !active.Request.Options[index].Enabled) return;
            active.Choice = index; navigator.Pop(view);
        }
        void Popped()
        {
            if (disposed || active == null) return;
            completed = active; active = null;
            int cancel = completed.Request.CancelIndex;
            if (!completed.Choice.HasValue && cancel >= 0 && completed.Request.Options[cancel].Enabled) completed.Choice = cancel;
        }
        void StackChanged()
        {
            if (disposed || completed == null) return;
            var result = completed; completed = null;
            try { if (result.Choice.HasValue) result.Chosen?.Invoke(result.Choice.Value); }
            catch (Exception e) { GameLog.Error("UI", "Confirm callback failed: " + e.Message); }
            finally { if (!disposed && active == null && queue.Count > 0) Begin(queue.Dequeue()); }
        }
        void ConfigureTooltip(Button button, ConfirmOptionViewModel option)
        {
            if (tooltipButtons.Remove(button)) tooltips.Detach(button);
            button.tooltip = option.Enabled ? null : option.DisabledReason;
            if (!option.Enabled && option.Visible && !string.IsNullOrEmpty(option.DisabledReason))
            {
                tooltips.Attach(button, content => { content.Title = option.Label; content.Body = option.DisabledReason; });
                tooltipButtons.Add(button);
            }
        }
        internal void Back()
        { if (active != null && active.Request.CancelIndex >= 0) Choose(active.Request.CancelIndex); }
        internal void Dispose()
        { if (disposed) return; disposed = true; navigator.StackChanged -= StackChanged; queue.Clear(); tooltipButtons.Clear(); active = completed = null; }
    }
}
