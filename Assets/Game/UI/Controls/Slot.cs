using Game.UI.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controls
{
    /// <summary>SPEC-60 §4.5: reusable icon tile with cached badges and cooldown geometry.</summary>
    [UxmlElement]
    public partial class Slot : VisualElement
    {
        readonly VisualElement icon, cooldown;
        readonly Label countLabel, keyLabel;
        Sprite sprite;
        int count = 1;
        float remaining;
        bool disabled, locked;
        string childNamePrefix;

        public Slot()
        {
            name = "slot"; focusable = true;
            AddToClassList(UIStyleClasses.Slot); AddToClassList(UIStyleClasses.IsEmpty);
            icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.AddToClassList(UIStyleClasses.SlotIcon);
            cooldown = new VisualElement { pickingMode = PickingMode.Ignore }; cooldown.AddToClassList(UIStyleClasses.SlotCooldown);
            countLabel = new Label { pickingMode = PickingMode.Ignore }; countLabel.AddToClassList(UIStyleClasses.SlotCount);
            keyLabel = new Label { pickingMode = PickingMode.Ignore }; keyLabel.AddToClassList(UIStyleClasses.SlotKey);
            cooldown.AddToClassList(UIStyleClasses.IsHidden); countLabel.AddToClassList(UIStyleClasses.IsHidden); keyLabel.AddToClassList(UIStyleClasses.IsHidden);
            Add(icon); Add(cooldown); Add(countLabel); Add(keyLabel);
            NameChildren(); RegisterCallback<AttachToPanelEvent>(OnAttach);
        }
        public void SetIcon(Sprite sprite)
        {
            if (this.sprite == sprite) return;
            this.sprite = sprite; icon.style.backgroundImage = new StyleBackground(sprite);
            EnableInClassList(UIStyleClasses.IsEmpty, sprite == null); RefreshReady();
        }
        public void SetCount(int count)
        {
            if (this.count == count) return;
            this.count = count;
            countLabel.text = count > 1 ? NumberStringCache.Get(count) : string.Empty;
            countLabel.EnableInClassList(UIStyleClasses.IsHidden, count <= 1);
        }
        public void SetKeyHint(string text)
        {
            keyLabel.text = text ?? string.Empty;
            keyLabel.EnableInClassList(UIStyleClasses.IsHidden, string.IsNullOrEmpty(text));
        }
        public void SetCooldown(float remainingFraction)
        {
            float value = float.IsNaN(remainingFraction) || float.IsInfinity(remainingFraction) ? 0 : Mathf.Clamp01(remainingFraction);
            if (remaining == value) return;
            remaining = value; cooldown.style.height = Length.Percent(value * 100);
            cooldown.EnableInClassList(UIStyleClasses.IsHidden, value == 0); RefreshReady();
        }
        public void SetState(bool selected, bool disabled, bool locked)
        {
            this.disabled = disabled; this.locked = locked;
            EnableInClassList(UIStyleClasses.IsSelected, selected);
            EnableInClassList(UIStyleClasses.IsDisabled, disabled);
            EnableInClassList(UIStyleClasses.IsLocked, locked);
            SetEnabled(!disabled && !locked); RefreshReady();
        }
        void RefreshReady() => EnableInClassList(UIStyleClasses.IsReady, sprite != null && remaining == 0 && !disabled && !locked);
        void OnAttach(AttachToPanelEvent evt) => NameChildren();
        void NameChildren()
        {
            if (childNamePrefix == name) return;
            childNamePrefix = name;
            icon.name = name + "-icon"; cooldown.name = name + "-cooldown";
            countLabel.name = name + "-count"; keyLabel.name = name + "-key";
        }
    }
}
