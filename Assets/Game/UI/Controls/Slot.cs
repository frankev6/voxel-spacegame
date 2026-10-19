using Game.UI.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controls
{
    /// <summary>SPEC-60 §4.5: reusable icon tile with cached badges and cooldown geometry.</summary>
    [UxmlElement]
    public partial class Slot : VisualElement
    {
        readonly VisualElement icon, cooldown, channel;
        readonly Label countLabel, keyLabel, noteLabel;
        Sprite sprite;
        int count = 1;
        float remaining, channelLeft;
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
            channel = new VisualElement { pickingMode = PickingMode.Ignore }; channel.AddToClassList(UIStyleClasses.SlotChannel); channel.generateVisualContent += DrawChannel;
            noteLabel = new Label { pickingMode = PickingMode.Ignore }; noteLabel.AddToClassList(UIStyleClasses.SlotNote);
            cooldown.AddToClassList(UIStyleClasses.IsHidden); countLabel.AddToClassList(UIStyleClasses.IsHidden); keyLabel.AddToClassList(UIStyleClasses.IsHidden);
            channel.AddToClassList(UIStyleClasses.IsHidden); noteLabel.AddToClassList(UIStyleClasses.IsHidden);
            Add(icon); Add(cooldown); Add(countLabel); Add(keyLabel); Add(channel); Add(noteLabel);
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
        /// <summary>ABL-006: the share of a running ability's time left, drawn clockwise just inside the tile's border from the top; 0 hides it.</summary>
        public void SetChannel(float leftFraction)
        {
            float value = float.IsNaN(leftFraction) || float.IsInfinity(leftFraction) ? 0 : Mathf.Clamp01(leftFraction);
            if (channelLeft == value) return;
            channelLeft = value; channel.EnableInClassList(UIStyleClasses.IsHidden, value == 0); channel.MarkDirtyRepaint();
        }
        /// <summary>A short reason across the foot of the tile ("NO HULL"); empty hides it.</summary>
        public void SetNote(string text)
        {
            text ??= string.Empty;
            if (noteLabel.text == text) return;
            noteLabel.text = text; noteLabel.EnableInClassList(UIStyleClasses.IsHidden, text.Length == 0);
        }
        void DrawChannel(MeshGenerationContext context)
        {
            var rect = channel.contentRect; if (channelLeft <= 0 || rect.width <= 0 || rect.height <= 0) return;
            const float width = 3; float inset = width * .5f;
            float l = rect.xMin + inset, t = rect.yMin + inset, r = rect.xMax - inset, b = rect.yMax - inset, w = r - l, h = b - t;
            float length = channelLeft * 2 * (w + h);
            var painter = context.painter2D; painter.lineWidth = width; painter.strokeColor = channel.resolvedStyle.color; painter.lineCap = LineCap.Butt;
            painter.BeginPath(); var point = new Vector2(l + w * .5f, t); painter.MoveTo(point);
            // clockwise from the top centre: the top's right half, right, bottom, left, the top's left half
            Vector2[] corners = { new(r, t), new(r, b), new(l, b), new(l, t), new(l + w * .5f, t) };
            foreach (var corner in corners)
            {
                float edge = Vector2.Distance(point, corner);
                if (length <= edge) { painter.LineTo(Vector2.Lerp(point, corner, edge > 0 ? length / edge : 0)); break; }
                painter.LineTo(corner); length -= edge; point = corner;
            }
            painter.Stroke();
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
            channel.name = name + "-channel"; noteLabel.name = name + "-note";
        }
    }
}
