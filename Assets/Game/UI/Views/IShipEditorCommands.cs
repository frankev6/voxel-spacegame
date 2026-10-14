namespace Game.UI.Views
{
    /// <summary>Implements SPEC-51 §4.4. Editor commands; implemented by the App presenter.</summary>
    public interface IShipEditorCommands
    {
        void SelectTool(int toolId);
        void SelectBlock(string blockId);
        void SelectColor(int index);
        void SelectCategory(int index);
        void SetSearch(string text);
        void Undo();
        void Redo();
        void ToggleMirror(int axis); // 0 X, 1 Y, 2 Z
        void NudgeMirrorPlane(int axis, int halfCells);
        void ToggleSlice();
        void StepSlice(int delta);
        void ToggleXRay();
        void CycleBoxMode();
        void FocusIssue(int index);
        void RemoveFloatingParts();
        void RevertAll();
        void SaveBlueprint();
        void OpenBlueprintBrowser();
        void RequestCommit();
        void RequestLeave();
        void OpenSettings();
        bool OnBackPressed(); // cancel chain; always returns true
        /// QA-001 creative hotbar and drawers.
        void SelectHotbar(int slot);
        void TogglePalette();
        void ToggleDetails();
    }
}
