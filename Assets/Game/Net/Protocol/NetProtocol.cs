namespace Game.Net.Protocol
{
    /// <summary>Wire constants (SPEC-90 §3.5). Bump <see cref="Version"/> whenever any message layout changes.</summary>
    public static class NetProtocol
    {
        public const uint Version = 8;
        public const ushort DefaultPort = 7777;
        public const int MaxPlayers = 4;                 // host + 3 guests
        public const int UnreliablePacketBytes = 1200;   // below every common path MTU, no fragmentation
        public const int ReliableChunkBytes = 8 * 1024;  // one reliable send; the fragmentation stage splits it
        public const int SnapshotEveryTicks = 2;         // 30 Hz at the 60 Hz fixed step
        public const int InterpolationDelayTicks = 6;    // 100 ms behind the newest host tick
        public const int MaxExtrapolationTicks = 12;     // 200 ms
        public const float PingIntervalSeconds = 0.5f;
    }

    /// <summary>One byte at the head of every message. Reliable kinds travel in the ordered stream, unreliable kinds one per packet.</summary>
    public enum NetMessage : byte
    {
        // guest → host, reliable
        Hello = 1, Ready = 2,
        // host → guest, reliable
        Welcome = 10, Reject = 11, JoinBegin = 12, JoinEnd = 13,
        GridSpawn = 20, GridDespawn = 21, GridDelta = 22, GridState = 23, AbilityState = 24, GridSalvage = 25, PilotStatus = 26,
        ChunkSpawn = 30, ChunkDespawn = 31,
        CombatFx = 40, HashReport = 41, Notice = 42,
        // dev builds: host → guest command, guest → host reply (reliable)
        DevCommand = 50, DevResult = 51,
        // unreliable, both ways
        Ping = 100, Pong = 101,
        Goodbye = 102,                                   // sent three times on leave, so loss or a closing driver cannot eat it
        // guest → host, unreliable
        Command = 110,
        // host → guest, unreliable
        Snapshot = 120,
    }
}
