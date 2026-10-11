using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. Dense brick table on the root.</summary>
    [InternalBufferCapacity(0)] public struct BrickSlot : IBufferElementData { public Entity Brick; }
}
