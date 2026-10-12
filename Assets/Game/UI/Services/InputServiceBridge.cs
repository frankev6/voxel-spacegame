namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §6. Contract shell; implementation follows in the owning UI task.</summary>
    public sealed class InputServiceBridge : IInputContextTarget, System.IDisposable
    {
        public InputServiceBridge(Game.Input.InputService input) => throw new System.NotImplementedException();
        public void SetContext(Game.Core.InputContext context) => throw new System.NotImplementedException();                   // → input.SetContext(context)
        public void BindBack(System.Action onBack) => throw new System.NotImplementedException();                               // subscribes Controls.Global.Back.performed
        public string GetBindingDisplay(string actionPath) => throw new System.NotImplementedException();                      // → SPEC-03 display string, e.g. "E"
        public void Dispose() => throw new System.NotImplementedException();                                                    // unsubscribes
    }
}
