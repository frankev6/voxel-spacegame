namespace Game.Voxel.Data
{
    /// <summary>Implements SPEC-05 §4.7.</summary>
    public struct VoxsEncodeOptions
    {
        public bool Deflate;                                  // true for assets/clipboard, false inside save chunks (container deflates)
        public static VoxsEncodeOptions ForSaveChunk => new VoxsEncodeOptions { Deflate = false };
        public static VoxsEncodeOptions ForAsset => new VoxsEncodeOptions { Deflate = true };
    }
}
