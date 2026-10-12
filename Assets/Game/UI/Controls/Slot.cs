namespace Game.UI.Controls
{
    /// <summary>Implements SPEC-60 §4.5. Contract shell; implementation follows in the owning UI task.</summary>
    [UnityEngine.UIElements.UxmlElement]
    public partial class Slot : UnityEngine.UIElements.VisualElement
    {
        public void SetIcon(UnityEngine.Sprite sprite) => throw new System.NotImplementedException();    // null → .is-empty
        public void SetCount(int count) => throw new System.NotImplementedException();                   // count <= 1 hides the badge; uses NumberStringCache
        public void SetKeyHint(string text) => throw new System.NotImplementedException();               // null hides
        public void SetCooldown(float remainingFraction) => throw new System.NotImplementedException();  // 0 hides overlay; overlay height = fraction %
        public void SetState(bool selected, bool disabled, bool locked) => throw new System.NotImplementedException();
    }
}
