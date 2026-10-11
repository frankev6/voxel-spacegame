namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5. One image to store in the slot's thumbs folder. Pixels are RGBA32, bottom row first (Texture2D layout).</summary>
    public sealed class ThumbnailRequest { public string Key; public byte[] Rgba32; public int Width; public int Height; public bool Pending; }
}
