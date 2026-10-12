namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §6. Contract shell; implementation follows in the owning UI task.</summary>
    public sealed class TooltipService
    {
        public void Attach(UnityEngine.UIElements.VisualElement target, System.Action<Game.UI.Data.TooltipContent> fill) => throw new System.NotImplementedException();
        public void Detach(UnityEngine.UIElements.VisualElement target) => throw new System.NotImplementedException();
        public void ShowAtScreen(System.Action<Game.UI.Data.TooltipContent> fill, UnityEngine.Vector2 screenPosBottomLeft) => throw new System.NotImplementedException();
        public void HideScreenTooltip() => throw new System.NotImplementedException();
        public bool IsVisible { get => throw new System.NotImplementedException(); }
    }
}
