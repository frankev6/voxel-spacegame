namespace Game.UI.Controls
{
    /// <summary>Implements SPEC-60 §4.5. Contract shell; implementation follows in the owning UI task.</summary>
    [UnityEngine.UIElements.UxmlElement]
    public partial class TabBar : UnityEngine.UIElements.VisualElement
    {
        public int SelectedIndex { get; set; }             // setting fires SelectionChanged only when the value changes
        public event System.Action<int> SelectionChanged { add => throw new System.NotImplementedException(); remove => throw new System.NotImplementedException(); }
        /// Rebuilds tab buttons (allowed only outside per-frame paths). names[i] becomes the button name.
        public void SetTabs(System.Collections.Generic.IReadOnlyList<string> labels, System.Collections.Generic.IReadOnlyList<string> names) => throw new System.NotImplementedException();
        public int TabCount { get => throw new System.NotImplementedException(); }
    }
}
