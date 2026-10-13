namespace Game.Core.Settings
{
    /// <summary>Control settings (SPEC-02 §4.9).</summary>
    public sealed class ControlSettingsData
    {
        public float MouseSensitivity = 1f;          // [0.1, 5]
        public bool InvertCameraY = false;
        public byte CameraYawModeIndex = 0;         // 0 FollowHeading, 1 Free (ADR-0073)
        public float CameraZoomSensitivity = 1f;     // [0.1, 5]
        public string BindingOverridesJson = "";     // InputActionAsset.SaveBindingOverridesAsJson(); null → ""
        public void CopyFrom(ControlSettingsData o)
        {
            MouseSensitivity = o.MouseSensitivity; InvertCameraY = o.InvertCameraY; CameraZoomSensitivity = o.CameraZoomSensitivity;
            BindingOverridesJson = o.BindingOverridesJson; CameraYawModeIndex = o.CameraYawModeIndex;
        }
        public void Clamp()
        {
            var d = new ControlSettingsData();
            MouseSensitivity = SettingsClamp.Range(MouseSensitivity, 0.1f, 5f, d.MouseSensitivity);
            CameraZoomSensitivity = SettingsClamp.Range(CameraZoomSensitivity, 0.1f, 5f, d.CameraZoomSensitivity);
            BindingOverridesJson ??= "";
            if (CameraYawModeIndex > 1) CameraYawModeIndex = 0;
        }
        public bool ValueEquals(ControlSettingsData o)
        {
            return MouseSensitivity == o.MouseSensitivity && InvertCameraY == o.InvertCameraY && CameraZoomSensitivity == o.CameraZoomSensitivity
                && CameraYawModeIndex == o.CameraYawModeIndex && (BindingOverridesJson ?? "") == (o.BindingOverridesJson ?? "");
        }
    }
}
