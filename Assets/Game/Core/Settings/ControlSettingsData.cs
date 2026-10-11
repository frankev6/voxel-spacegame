namespace Game.Core.Settings
{
    /// <summary>Control settings (SPEC-02 §4.9).</summary>
    public sealed class ControlSettingsData
    {
        public float MouseSensitivity = 1f;          // [0.1, 5]
        public bool InvertCameraY = false;
        public float CameraZoomSensitivity = 1f;     // [0.1, 5]
        public string BindingOverridesJson = "";     // InputActionAsset.SaveBindingOverridesAsJson(); null → ""
        public void CopyFrom(ControlSettingsData o)
        {
            MouseSensitivity = o.MouseSensitivity; InvertCameraY = o.InvertCameraY; CameraZoomSensitivity = o.CameraZoomSensitivity;
            BindingOverridesJson = o.BindingOverridesJson;
        }
        public void Clamp()
        {
            var d = new ControlSettingsData();
            MouseSensitivity = SettingsClamp.Range(MouseSensitivity, 0.1f, 5f, d.MouseSensitivity);
            CameraZoomSensitivity = SettingsClamp.Range(CameraZoomSensitivity, 0.1f, 5f, d.CameraZoomSensitivity);
            BindingOverridesJson ??= "";
        }
        public bool ValueEquals(ControlSettingsData o)
        {
            return MouseSensitivity == o.MouseSensitivity && InvertCameraY == o.InvertCameraY && CameraZoomSensitivity == o.CameraZoomSensitivity
                && (BindingOverridesJson ?? "") == (o.BindingOverridesJson ?? "");
        }
    }
}
