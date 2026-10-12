namespace Game.ShipEditor.Data
{
    [System.Flags]
    public enum EditorActions : ulong
    {
        None = 0, Undo = 1ul << 0, Redo = 1ul << 1, Copy = 1ul << 2, Cut = 1ul << 3, Paste = 1ul << 4, Delete = 1ul << 5,
        RotateYaw = 1ul << 6, RotatePitch = 1ul << 7, RotateRoll = 1ul << 8,
        ToolPlace = 1ul << 9, ToolLine = 1ul << 10, ToolBox = 1ul << 11, ToolFill = 1ul << 12, ToolRemove = 1ul << 13,
        ToolReplace = 1ul << 14, ToolPaint = 1ul << 15, ToolEyedropper = 1ul << 16, ToolSelect = 1ul << 17,
        MirrorX = 1ul << 18, MirrorY = 1ul << 19, MirrorZ = 1ul << 20, SliceToggle = 1ul << 21, SliceUp = 1ul << 22, SliceDown = 1ul << 23,
        XRayToggle = 1ul << 24, BoxModeCycle = 1ul << 25, MoveLeft = 1ul << 26, MoveRight = 1ul << 27, MoveForward = 1ul << 28,
        MoveBack = 1ul << 29, MoveUp = 1ul << 30, MoveDown = 1ul << 31, MirrorSelection = 1ul << 32, SelectAll = 1ul << 33
    }
}
