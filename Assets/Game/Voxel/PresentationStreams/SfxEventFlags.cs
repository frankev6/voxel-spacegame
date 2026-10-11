namespace Game.Voxel.PresentationStreams
{
    /// <summary>Implements SPEC-64 §4.1.</summary>
    [System.Flags]
    public enum SfxEventFlags : byte
    {
        None = 0,
        PlayerSource = 1,   // emitted by or on the player ship: never distance-culled, sorts before equal priority
    }
}
