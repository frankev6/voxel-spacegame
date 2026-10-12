namespace Game.UI.Controls
{
    /// <summary>Implements SPEC-60 §4.5. Contract shell; implementation follows in the owning UI task.</summary>
    [UnityEngine.UIElements.UxmlElement]
    public partial class Meter : UnityEngine.UIElements.VisualElement
    {
        [UnityEngine.UIElements.UxmlAttribute] public string Caption { get; set; }      // static text, e.g. "HULL"
        [UnityEngine.UIElements.UxmlAttribute] public string Variant { get; set; }      // "hull" | "shield" | "energy" | "neutral" → class .meter--<variant>
        [UnityEngine.UIElements.UxmlAttribute] public bool ShowNumbers { get; set; } = true;
        public float Fraction { get => throw new System.NotImplementedException(); }                     // 0..1
        /// Sets fill width as percent (rounded to 0.1 %) and value/max labels from NumberStringCache. No allocation.
        public void SetValue(int value, int max) => throw new System.NotImplementedException();
        public void SetFraction(float fraction) => throw new System.NotImplementedException();          // fill only, labels untouched
        /// Adds .meter--low when fraction < lowThreshold (default 0.25).
        public float LowThreshold { get; set; }
    }
}
