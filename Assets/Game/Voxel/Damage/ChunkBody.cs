using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>Pooled chunk body (not a grid). Writer: SplitSystem.</summary>
    public struct ChunkBody : IComponentData
    {
        public uint SpawnTick, DissolveStartTick, EndTick;
        public float CellSize;
        public float3 LocalOrigin;     // body-local position of cell (0,0,0)'s min corner (COM is the body origin)
        public ushort CellCount;
        public byte Slot;              // pool slot 0..MaxLiveChunks-1
        public byte Live;              // 1 while enabled
    }
}
