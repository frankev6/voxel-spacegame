using Unity.Entities;

namespace Game.Voxel.Model
{
    /// <summary>ADR-0054: session-unique grid serials, allocated by structural spawn or the ordered split job.</summary>
    public struct GridSerialAllocator : IComponentData { public uint Next; }
}
