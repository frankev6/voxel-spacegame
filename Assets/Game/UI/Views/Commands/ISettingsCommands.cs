namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public interface ISettingsCommands
    {
        void SelectTab(int tab);
        void SelectControlsGroup(int group);   // HUD-021: the Controls page's sub-tab
        void SelectControlsDevice(int device); // HUD-021: 0 keyboard and mouse, 1 controller
        void SetFloat(SettingKey key, float value);
        void SetBool(SettingKey key, bool value);
        void SetIndex(SettingKey key, int index);
        void StartRebind(int row);
        void CancelRebind();
        void ResetBindings();
    }
}
