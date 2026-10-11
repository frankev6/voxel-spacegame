using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. Exactly 4096 entries.</summary>
    [InternalBufferCapacity(0)] public struct BrickCells : IBufferElementData { public Cell Value; }
}
