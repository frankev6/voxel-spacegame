using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. 4096 entries when dense, zero when BrickSparseCells owns storage (ADR-0023).</summary>
    [InternalBufferCapacity(0)] public struct BrickCells : IBufferElementData { public Cell Value; }
}
