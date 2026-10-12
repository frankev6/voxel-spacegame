namespace Game.UI.Controls
{
    /// <summary>Implements SPEC-60 §4.5. Contract shell; implementation follows in the owning UI task.</summary>
    [UnityEngine.UIElements.UxmlElement]
    public partial class KeyHint : UnityEngine.UIElements.Label
    {
        [UnityEngine.UIElements.UxmlAttribute] public string ActionPath { get; set; }   // "Flight/Interact"
        /// Resolver injected once by UIRoot; refreshed when SPEC-03 raises BindingsChanged.
        public void Refresh(System.Func<string, string> bindingDisplay) => throw new System.NotImplementedException();
    }
}
