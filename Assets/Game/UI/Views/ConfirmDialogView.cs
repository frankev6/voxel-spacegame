using System;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-60 §5.9: three cached named option controls over plain dialog state and injected commands.</summary>
    public sealed class ConfirmDialogView : UIScreen
    {
        readonly ConfirmDialogViewModel model;
        readonly Action<int> choose;
        readonly Action back, popped;
        readonly Action<Button, ConfirmOptionViewModel> configureTooltip;
        readonly Button[] buttons = new Button[3];
        internal ConfirmDialogView(ConfirmDialogViewModel model, Action<int> choose, Action back, Action popped,
            Action<Button, ConfirmOptionViewModel> configureTooltip)
        { this.model = model; this.choose = choose; this.back = back; this.popped = popped; this.configureTooltip = configureTooltip; }
        public override string Key => "confirm";
        public override UILayer Layer => UILayer.Modals;
        public override VisualElement DefaultFocus
        {
            get
            {
                int preferred = model.DefaultFocusIndex;
                if (model.Options[preferred].Visible && model.Options[preferred].Enabled) return buttons[preferred];
                for (int i = 0; i < buttons.Length; i++) if (model.Options[i].Visible && model.Options[i].Enabled) return buttons[i];
                return null;
            }
        }
        protected override void OnBuild()
        {
            Root.dataSource = model;
            Q<Label>("confirm-title"); Q<Label>("confirm-body");
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                var button = buttons[i] = Q<Button>("confirm-option-" + i);
                button.dataSource = model.Options[i]; button.clicked += () => choose(index);
            }
        }
        public override void OnPush(object args)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var option = model.Options[i]; var button = buttons[i];
                button.name = option.ElementName; button.SetEnabled(option.Enabled);
                button.EnableInClassList(UIStyleClasses.IsHidden, !option.Visible);
                button.EnableInClassList(UIStyleClasses.BtnPrimary, option.Variant == ButtonVariant.Primary);
                button.EnableInClassList(UIStyleClasses.BtnSecondary, option.Variant == ButtonVariant.Secondary);
                button.EnableInClassList(UIStyleClasses.BtnGhost, option.Variant == ButtonVariant.Ghost);
                button.EnableInClassList(UIStyleClasses.BtnDanger, option.Variant == ButtonVariant.Danger);
                button.EnableInClassList(UIStyleClasses.BtnIcon, option.Variant == ButtonVariant.Icon);
                configureTooltip(button, option);
            }
        }
        public override bool OnBack() { back(); return true; }
        public override void OnPop() => popped();
    }
}
