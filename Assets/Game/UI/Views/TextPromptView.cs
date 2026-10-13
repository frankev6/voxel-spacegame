using System;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-60 §5.9: bindable prompt controls with valid Enter submission and navigator-owned cancellation.</summary>
    public sealed class TextPromptView : UIScreen
    {
        readonly TextPromptViewModel model;
        readonly Action<string> changeText;
        readonly Action submit, cancel, popped;
        TextField field;
        Button ok;
        VisualElement errorRow;
        bool configuring;
        internal TextPromptView(TextPromptViewModel model, Action<string> changeText, Action submit, Action cancel, Action popped)
        { this.model = model; this.changeText = changeText; this.submit = submit; this.cancel = cancel; this.popped = popped; }
        public override string Key => "text-prompt";
        public override UILayer Layer => UILayer.Modals;
        public override VisualElement DefaultFocus => field;
        internal bool HasFocusedInput
        {
            get
            {
                var focused = field.panel?.focusController?.focusedElement as VisualElement;
                return focused == field || (focused != null && field.Contains(focused));
            }
        }
        internal void BlurInput() => field.Blur();
        protected override void OnBuild()
        {
            Root.dataSource = model;
            Q<Label>("text-prompt-title"); Q<Label>("text-prompt-label");
            field = Q<TextField>("text-prompt-field"); Q<Label>("text-prompt-error"); errorRow = Q<VisualElement>("text-prompt-error-row"); ok = Q<Button>("text-prompt-ok");
            field.RegisterValueChangedCallback(evt => { if (!configuring) changeText(evt.newValue); });
            field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            ok.clicked += submit; Q<Button>("text-prompt-cancel").clicked += cancel;
            model.propertyChanged += OnModelChanged;
        }
        public override void OnPush(object args)
        {
            // Changing maxLength can normalize the old field value; it must not replace the new request's text.
            configuring = true;
            try { field.maxLength = model.MaxLength; field.SetValueWithoutNotify(model.Text); RefreshValidation(); }
            finally { configuring = false; }
        }
        void OnModelChanged(object sender, BindablePropertyChangedEventArgs args) => RefreshValidation();
        void RefreshValidation()
        {
            ok.SetEnabled(model.CanSubmit);
            errorRow.EnableInClassList(UIStyleClasses.IsHidden, model.Error == null);
            field.EnableInClassList(UIStyleClasses.IsInvalid, !model.CanSubmit);
        }
        void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
            // Consume Enter before the built-in field can change text or submit a second time.
            evt.StopImmediatePropagation(); field.panel?.focusController?.IgnoreEvent(evt);
            if (model.CanSubmit) submit();
        }
        public override bool OnBack() { cancel(); return true; }
        public override void OnPop() { field.Blur(); popped(); }
    }
}
