using Unity.Entities;

namespace Game.Net.Replication
{
    /// <summary>On a guest: a grid root or chunk body whose state comes from the host (SPEC-90 §4).</summary>
    public struct Replicated : IComponentData
    {
        public ulong NetId;
        public uint LastSnapshotTick;
        public byte OwnedLocally;      // 1 on the guest's own ship: ShipCommand comes from local input, not snapshots
    }

    /// <summary>Network identities (ADR-0116 §5). Grids use their PersistentId; chunk bodies set the top bit.</summary>
    public static class NetIds
    {
        public const ulong ChunkBit = 1UL << 63;
        public static ulong Chunk(byte slot, uint spawnTick) => ChunkBit | ((ulong)slot << 32) | spawnTick;
        public static bool IsChunk(ulong id) => (id & ChunkBit) != 0;
    }

    /// <summary>What a guest should create for a host grid (mapped to a GridProfile on the guest).</summary>
    public enum GridRoleHint : byte { None = 0, Ship = 1, Station = 2 }
}
