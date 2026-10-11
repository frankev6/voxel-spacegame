namespace Game.Persistence.Container
{
    /// <summary>Implements SPEC-05 §4.5 (byte layout §4.2).</summary>
    public struct ContainerHeader
    {
        public ushort FormatVersion; public ushort HeaderFlags; public uint DatabaseHash32;
        public long SavedUtcTicks; public double PlaytimeSeconds; public uint ChunkCount; public uint HeaderCrc32;
        public const int Size = 36; public const ushort CurrentFormatVersion = 1;
    }
}
