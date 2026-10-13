using Unity.Collections;
using Unity.Entities;
namespace Game.Presentation.Vfx
{
    /// <summary>Owned by ChunkDissolveSyncSystem; presentation reads; its owning system resets last update's count.</summary>
    internal struct ChunkDissolveStats : IComponentData { public NativeReference<int> Count; }
}
