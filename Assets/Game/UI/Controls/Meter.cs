using Game.UI.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controls
{
    /// <summary>SPEC-60 §4.5: cached number labels and data-driven fill geometry.</summary>
    [UxmlElement]
    public partial class Meter : VisualElement
    {
        readonly VisualElement fill;
        readonly Label captionLabel, valueLabel, maxLabel;
        string caption = string.Empty, variant = "neutral", childNamePrefix;
        bool showNumbers = true;
        float fraction, percent = -1, lowThreshold = .25f;
        bool absent;
        int lastValue, lastMax;

        public Meter()
        {
            name = "meter"; pickingMode = PickingMode.Ignore;
            AddToClassList(UIStyleClasses.Meter); AddToClassList(UIStyleClasses.MeterNeutral);
            // QA-001 NOCTURNE: a hairline track under the fill (styled by nocturne-skin.uss).
            var track = new VisualElement { pickingMode = PickingMode.Ignore }; track.AddToClassList("meter__track"); Add(track);
            fill = new VisualElement { pickingMode = PickingMode.Ignore }; fill.AddToClassList(UIStyleClasses.MeterFill);
            captionLabel = new Label { pickingMode = PickingMode.Ignore }; captionLabel.AddToClassList(UIStyleClasses.MeterCaption);
            valueLabel = new Label(NumberStringCache.Get(0)) { pickingMode = PickingMode.Ignore }; valueLabel.AddToClassList(UIStyleClasses.MeterValue);
            maxLabel = new Label(NumberStringCache.Get(0)) { pickingMode = PickingMode.Ignore }; maxLabel.AddToClassList(UIStyleClasses.MeterMax);
            Add(fill); Add(captionLabel); Add(valueLabel); Add(maxLabel);
            NameChildren(); RegisterCallback<AttachToPanelEvent>(OnAttach);
            SetFraction(0);
        }

        [UxmlAttribute] public string Caption
        {
            get => caption;
            set { caption = value ?? string.Empty; captionLabel.text = caption; }
        }
        [UxmlAttribute] public string Variant
        {
            get => variant;
            set
            {
                string next = value == "hull" || value == "shield" || value == "energy" ? value : "neutral";
                if (variant == next) return;
                RemoveFromClassList(VariantClass(variant)); variant = next; AddToClassList(VariantClass(next));
            }
        }
        [UxmlAttribute] public bool ShowNumbers
        {
            get => showNumbers;
            set
            {
                if (showNumbers == value) return;
                showNumbers = value;
                valueLabel.EnableInClassList(UIStyleClasses.IsHidden, !value);
                maxLabel.EnableInClassList(UIStyleClasses.IsHidden, !value);
            }
        }
        public float Fraction => fraction;
        public float LowThreshold
        {
            get => lowThreshold;
            set { lowThreshold = ClampFraction(value); EnableInClassList(UIStyleClasses.MeterLow, fraction < lowThreshold); }
        }
        public void SetValue(int value, int max)
        {
            if (value != lastValue) { lastValue = value; valueLabel.text = NumberStringCache.Get(value); }
            if (max != lastMax) { lastMax = max; maxLabel.text = NumberStringCache.Get(max); }
            // QA-001: a ship without the system (max 0) shows a muted, empty meter rather than a red low warning.
            absent = max <= 0; EnableInClassList("meter--absent", absent);
            SetFraction(max > 0 ? (float)value / max : 0);
        }
        public void SetFraction(float fraction)
        {
            this.fraction = ClampFraction(fraction);
            float rounded = Mathf.Round(this.fraction * 1000) * .1f;
            if (rounded != percent) { percent = rounded; fill.style.width = Length.Percent(rounded); }
            EnableInClassList(UIStyleClasses.MeterLow, !absent && this.fraction < lowThreshold);
        }
        static float ClampFraction(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value);
        static string VariantClass(string value) => value switch
        {
            "hull" => UIStyleClasses.MeterHull, "shield" => UIStyleClasses.MeterShield,
            "energy" => UIStyleClasses.MeterEnergy, _ => UIStyleClasses.MeterNeutral
        };
        void OnAttach(AttachToPanelEvent evt) => NameChildren();
        void NameChildren()
        {
            if (childNamePrefix == name) return;
            childNamePrefix = name;
            fill.name = name + "-fill"; captionLabel.name = name + "-caption";
            valueLabel.name = name + "-value"; maxLabel.name = name + "-max";
        }
    }
}
