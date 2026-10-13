namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public enum ToolId : byte
    {
        Place = 1, Line = 2, Box = 3, Fill = 4, Remove = 5, Replace = 6, Paint = 7, Eyedropper = 8, Select = 9, Paste = 10
    }
}
