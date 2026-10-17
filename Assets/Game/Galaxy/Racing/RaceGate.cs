using Unity.Entities;
using Unity.Mathematics;

namespace Game.Galaxy.Racing
{
    /// <summary>One gate of the running track, on the RaceState entity (index = gate id). Positions are relative to the race entity's
    /// LocalTransform (the track origin), which OriginRebaseSystem shifts like any root. Written by TrackSpawnService.</summary>
    [InternalBufferCapacity(0)]
    public struct RaceGate : IBufferElementData
    {
        public float2 Position; public float Yaw; public float Width; public byte Required;
        public ulong NextMask;              // bit n: gate n may follow this one (≤ 64 gates)
        public float2 RespawnPosition; public float RespawnYaw;
    }

    /// <summary>On every grid a track spawned (wrecks, walls, obstacles), so a race can despawn its group.</summary>
    public struct TrackPieceOf : IComponentData { public ushort Group; }

    /// <summary>Start-grid slots of the running track, on the RaceState entity (relative to the track origin).</summary>
    [InternalBufferCapacity(4)]
    public struct RaceSlot : IBufferElementData { public float2 Position; public float Yaw; }
}
