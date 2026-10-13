using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;
using Game.UI.Views;

namespace Game.UI.Services
{
    /// <summary>SPEC-60 §5.9: trimmed validated text, FIFO prompt requests and post-pop completion.</summary>
    public sealed class TextPrompt
    {
        sealed class Pending
        {
            internal TextPromptRequest Request;
            internal Action<string> Submit;
            internal Action Cancel;
            internal string Result;
            internal bool Closing;
        }
        readonly UINavigator navigator;
        readonly TextPromptViewModel model = new TextPromptViewModel();
        readonly TextPromptView view;
        readonly Queue<Pending> queue = new Queue<Pending>();
        Pending active, completed;
        bool disposed;
        internal TextPrompt(UINavigator navigator)
        {
            this.navigator = navigator;
            view = new TextPromptView(model, ChangeText, Submit, Cancel, Popped);
            navigator.Register(view); navigator.StackChanged += StackChanged;
        }
        public bool IsOpen => active != null;
        public void Show(TextPromptRequest request, Action<string> onSubmit, Action onCancel)
        {
            if (disposed) throw new ObjectDisposedException(nameof(TextPrompt));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.MaxLength < 1) throw new ArgumentException("Text prompt MaxLength must be positive.", nameof(request));
            var next = new Pending { Request = new TextPromptRequest { Title = request.Title, Label = request.Label, InitialText = request.InitialText,
                MaxLength = request.MaxLength, OkLabel = request.OkLabel, CancelLabel = request.CancelLabel, Validate = request.Validate }, Submit = onSubmit, Cancel = onCancel };
            if (active != null) queue.Enqueue(next); else Begin(next);
        }
        void Begin(Pending next)
        {
            active = next;
            var req = next.Request; model.Title = req.Title ?? ""; model.Label = req.Label ?? "";
            model.OkLabel = req.OkLabel ?? "OK"; model.CancelLabel = req.CancelLabel ?? "Cancel"; model.MaxLength = req.MaxLength;
            ChangeText(req.InitialText ?? "");
            try { navigator.Push(view); }
            catch { active = null; throw; }
        }
        void ChangeText(string text)
        {
            if (disposed || active == null || active.Closing) return;
            text ??= "";
            model.Text = text.Length > model.MaxLength ? text.Substring(0, model.MaxLength) : text;
            ValidateText();
        }
        string ValidateText()
        {
            var current = active;
            string trimmed = model.Text.Trim();
            string error;
            try { error = trimmed.Length == 0 ? "Required" : current.Request.Validate?.Invoke(trimmed); }
            catch (Exception e) { GameLog.Error("UI", "Text validation failed: " + e.Message); error = "Validation failed"; }
            if (ReferenceEquals(current, active)) { model.Error = error; model.CanSubmit = error == null; }
            return trimmed;
        }
        void Submit()
        {
            if (disposed || active == null || !view.IsOpen || active.Closing) return;
            var current = active;
            string trimmed = ValidateText(); if (disposed || !ReferenceEquals(current, active) || !model.CanSubmit) return;
            active.Result = trimmed; active.Closing = true; CloseAfterBlur(current);
        }
        void Cancel()
        {
            if (disposed || active == null || !view.IsOpen || active.Closing) return;
            var current = active; active.Closing = true; CloseAfterBlur(current);
        }
        void CloseAfterBlur(Pending current)
        {
            // During a UI event, Blur queues focus events. Keep the modal paused until
            // the root's focus tracker restores context, then pop before invoking its callback.
            view.BlurInput();
            if (ReferenceEquals(active, current) && view.IsOpen &&
                (navigator.CurrentContext != InputContext.TextEntry || !view.HasFocusedInput)) navigator.Pop(view);
        }
        void Popped() { if (disposed || active == null) return; completed = active; active = null; }
        void StackChanged()
        {
            if (disposed) return;
            if (active != null && active.Closing && view.IsOpen &&
                (navigator.CurrentContext != InputContext.TextEntry || !view.HasFocusedInput))
            { navigator.Pop(view); return; }
            if (completed == null) return;
            var result = completed; completed = null;
            try { if (result.Result == null) result.Cancel?.Invoke(); else result.Submit?.Invoke(result.Result); }
            catch (Exception e) { GameLog.Error("UI", "Text prompt callback failed: " + e.Message); }
            finally { if (!disposed && active == null && queue.Count > 0) Begin(queue.Dequeue()); }
        }
        internal void Dispose()
        { if (disposed) return; disposed = true; navigator.StackChanged -= StackChanged; queue.Clear(); active = completed = null; }
    }
}
