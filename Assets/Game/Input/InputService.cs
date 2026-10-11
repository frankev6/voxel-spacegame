namespace Game.Input
{
    /// <summary>Owner of the Controls instance and the active input context (architecture §8). Main thread. Implements SPEC-03 §4.3 and §5.1.</summary>
    public sealed class InputService : System.IDisposable
    {
        public InputService(Game.Core.Settings.ControlSettingsData controls) { throw new System.NotImplementedException("INP-002"); }   // creates Controls, applies overrides, enables Global
        public Controls Controls => throw new System.NotImplementedException("INP-002");                                                    // generated class; read actions from here
        public Game.Core.InputContext CurrentContext => throw new System.NotImplementedException("INP-002");
        public void SetContext(Game.Core.InputContext context) { throw new System.NotImplementedException("INP-002"); }                    // only UINavigator (via SPEC-60's InputServiceBridge) calls this
        public string GetBindingDisplay(string actionPath) { throw new System.NotImplementedException("INP-002"); }                        // "Flight/Interact" → "F"; unknown → "?" + WarnOnce
#pragma warning disable CS0067 // raised by INP-003
        public event System.Action BindingsChanged;                                                                                         // after rebind, reset or ApplyBindingOverrides
#pragma warning restore CS0067
        public bool IsRebinding => throw new System.NotImplementedException("INP-003");
        public void StartRebind(string actionPath, int bindingIndex, System.Action<RebindResult> done) { throw new System.NotImplementedException("INP-003"); } // Esc cancels
        public void CancelRebind() { throw new System.NotImplementedException("INP-003"); }
        public string SaveBindingOverridesJson() { throw new System.NotImplementedException("INP-003"); }                                  // InputActionAsset.SaveBindingOverridesAsJson()
        public void ApplyBindingOverrides(string json) { throw new System.NotImplementedException("INP-003"); }                             // "" → RemoveAllBindingOverrides; invalid JSON → defaults + Warn
        public void ResetBinding(string actionPath, int bindingIndex) { throw new System.NotImplementedException("INP-003"); }
        public void ResetAllBindings() { throw new System.NotImplementedException("INP-003"); }
        public float MouseSensitivity { get => throw new System.NotImplementedException("INP-002"); set => throw new System.NotImplementedException("INP-002"); }   // [0.1, 5], from ControlSettingsData
        public bool InvertCameraY { get => throw new System.NotImplementedException("INP-002"); set => throw new System.NotImplementedException("INP-002"); }
        public float CameraZoomSensitivity { get => throw new System.NotImplementedException("INP-002"); set => throw new System.NotImplementedException("INP-002"); }
        public void Dispose() { throw new System.NotImplementedException("INP-002"); }                                                     // disables and disposes Controls
    }
}
