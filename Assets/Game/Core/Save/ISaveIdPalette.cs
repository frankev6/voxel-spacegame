namespace Game.Core.Save
{
    /// <summary>Save-wide ContentId palette, writer side (implemented by SPEC-05 SaveIdPalette). Implements SPEC-02 §4.7.</summary>
    public interface ISaveIdPalette { ushort GetOrAdd(in Game.Core.ContentId id); }
}
