namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §6. Contract shell; implementation follows in the owning UI task.</summary>
    public sealed class UIRoot : System.IDisposable
    {
        public UIRoot(UnityEngine.UIElements.UIDocument document, Game.UI.Data.UIConfig config,
                      Game.Core.IPauseService pause, IInputContextTarget contextTarget,
                      IUISoundSink sound, IFrameClock clock) => throw new System.NotImplementedException();
        public UnityEngine.UIElements.IPanel Panel { get => throw new System.NotImplementedException(); }
        public UnityEngine.UIElements.VisualElement RootElement { get => throw new System.NotImplementedException(); }          // #ui-root
        public UnityEngine.UIElements.VisualElement GetLayer(Game.UI.Data.UILayer layer) => throw new System.NotImplementedException();
        public UINavigator Navigator { get => throw new System.NotImplementedException(); }
        public TooltipService Tooltips { get => throw new System.NotImplementedException(); }
        public NotificationService Notifications { get => throw new System.NotImplementedException(); }
        public ConfirmDialog Confirm { get => throw new System.NotImplementedException(); }
        public TextPrompt Prompt { get => throw new System.NotImplementedException(); }
        public bool ReduceMotion { get => throw new System.NotImplementedException(); }
        public void Register(Game.UI.Views.UIScreen screen) => throw new System.NotImplementedException();                     // = Navigator.Register
        public void Tick(float unscaledDeltaTime) => throw new System.NotImplementedException();                                // once per frame from GameRoot.Update
        public void HandleBackPressed() => throw new System.NotImplementedException();                                          // = Navigator.Back()
        public bool IsWorldPointerAllowed(UnityEngine.Vector2 screenPosBottomLeft) => throw new System.NotImplementedException();
        public void ApplyAccessibility(in Game.UI.Data.AccessibilityOptions options) => throw new System.NotImplementedException();
        public void Dispose() => throw new System.NotImplementedException();                                                    // pops all, releases token, unregisters callbacks
    }
}
