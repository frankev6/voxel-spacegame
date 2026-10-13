using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;
using Unity.Mathematics;
namespace Game.Galaxy.Streaming
{
    /// <summary>Implements SPEC-31 §4.5. BeltSector contract; behaviour belongs to the owning streaming task.</summary>
    internal struct BeltSector
    {
        public ulong BodyKey;
        public int Sector;
        public float3 CentreLocal;
        public byte Loaded;
        public byte JobPending;
    }
}
