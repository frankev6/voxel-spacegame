namespace Game.Voxel.PresentationStreams
{
    /// <summary>Implements SPEC-63 §4.1; queue ownership follows ADR-0050.</summary>
    [System.Flags]
    public enum FxEventFlags : byte
    {
        None = 0,
        PaletteColor = 1,    // Color's low byte is a cell palette index (PaletteEntry.Color), resolved by FxUploadSystem
        PlayerSource = 2,    // emitted by or on the player ship: never distance-culled
        Collected = 4,       // QA-001 salvage pull: debris lives exactly FxCollect.Seconds, the flight time into the collector
    }

    /// <summary>Shared timing of salvage pull debris (simulation velocity and presentation lifetime must agree).</summary>
    public static class FxCollect { public const float Seconds = 0.5f, StaggerSeconds = 0.7f; }
}
