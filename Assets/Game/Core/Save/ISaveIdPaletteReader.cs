namespace Game.Core.Save
{
    /// <summary>Save-wide ContentId palette, reader side. Implements SPEC-02 §4.7.</summary>
    public interface ISaveIdPaletteReader { Game.Core.ContentId Get(ushort local); }
}
