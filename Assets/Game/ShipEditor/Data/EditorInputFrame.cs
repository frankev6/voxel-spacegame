namespace Game.ShipEditor.Data
{
    /// Sampled once per frame by the App host from SPEC-03's Editor map.
    public struct EditorInputFrame
    {
        public Unity.Mathematics.float2 PointerScreen;     // pixels, bottom-left origin
        public bool PointerAllowed;                        // UIRoot.IsWorldPointerAllowed(PointerScreen)
        public bool PrimaryPressed, PrimaryHeld, PrimaryReleased;
        public bool SecondaryPressed, SecondaryReleased;
        public bool Shift, Ctrl, Alt;
        public float ToolWheel;                            // scroll notches this frame, + = up
        public EditorActions Actions;                      // edge-triggered this frame
        public byte HotbarSlot;                            // QA-001: 1..9 = number key pressed this frame, 0 = none
        public bool TogglePalette;                         // QA-001: Tab
    }
}
