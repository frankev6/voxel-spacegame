namespace Game.Core.Settings
{
    /// <summary>Control settings (SPEC-02 §4.9).</summary>
    public sealed class ControlSettingsData
    {
        public float MouseSensitivity = 1f;          // [0.1, 5]
        public bool InvertCameraY = false;
        public float CameraZoomSensitivity = 1f;     // [0.1, 5]
        public string BindingOverridesJson = "";     // InputActionAsset.SaveBindingOverridesAsJson(); null → ""
        public void CopyFrom(ControlSettingsData o) { throw new System.NotImplementedException("CORE-011"); }
        public void Clamp() { throw new System.NotImplementedException("CORE-011"); }
        public bool ValueEquals(ControlSettingsData o) { throw new System.NotImplementedException("CORE-011"); }
    }
}
