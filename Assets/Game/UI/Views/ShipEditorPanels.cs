namespace Game.UI.Views
{
    /// <summary>Implements SPEC-51 §4.4. Pure construction entry point for the four editor regions.</summary>
    public static class ShipEditorPanels
    {
        // TODO(EDT-021): mount the authored panels once; EDT-001 intentionally supplies only the empty contract.
        public static void Mount(UnityEngine.UIElements.VisualElement top, UnityEngine.UIElements.VisualElement left,
            UnityEngine.UIElements.VisualElement right, UnityEngine.UIElements.VisualElement bottom,
            Game.UI.ViewModels.ShipEditorViewModel vm, IShipEditorCommands commands, Game.UI.Services.UIRoot ui)
        { }
    }
}
